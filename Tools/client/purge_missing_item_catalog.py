#!/usr/bin/env python3
"""Remove ItemCatalog / ItemCollection entries whose collection prefabs are missing on disk."""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PREFAB_ROOT = ROOT / "Assets/_Game/Content/ItemCollection/Prefabs"
CATALOG = ROOT / "Assets/_Game/Content/Resources/Items/ItemCatalog.asset"
MANIFEST = ROOT / "Assets/_Game/Content/ItemCollection/ItemCollection.json"

ITEM_BLOCK = re.compile(
    r"^    - id: (?P<id>\S+)\n"
    r"      displayName: .*?\n"
    r"      enabled: \d+\n"
    r"      prefab: \{[^\}]*\}\n?",
    re.M,
)


def existing_prefab_stems() -> set[str]:
    return {p.stem for p in PREFAB_ROOT.rglob("*.prefab")}


def valid_catalog_ids(stems: set[str]) -> set[str]:
    ids: set[str] = set()
    for stem in stems:
        if "_" in stem:
            ids.add("i" + stem.rsplit("_", 1)[-1])
        else:
            ids.add("i" + stem)
    return ids


def purge_manifest(stems: set[str]) -> tuple[int, int]:
    data = json.loads(MANIFEST.read_text(encoding="utf-8"))
    items = data.get("items") or []
    before = len(items)
    kept = []
    for entry in items:
        eid = entry.get("id") or ""
        category = entry.get("category") or ""
        if eid in stems and (PREFAB_ROOT / category / f"{eid}.prefab").is_file():
            kept.append(entry)
    data["items"] = kept
    # Match existing pretty style used by importers
    MANIFEST.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return before - len(kept), len(kept)


def purge_catalog(valid_ids: set[str]) -> tuple[int, dict[str, int]]:
    text = CATALOG.read_text(encoding="utf-8")
    # Split categories at top-level "  - id:"
    parts = re.split(r"(?=^  - id: )", text, flags=re.M)
    header = parts[0]
    per_cat: dict[str, int] = {}
    removed_total = 0
    out = [header]
    for part in parts[1:]:
        cat_m = re.match(r"^  - id: (\S+)", part)
        cat_id = cat_m.group(1) if cat_m else "?"
        removed = 0

        def repl(m: re.Match[str]) -> str:
            nonlocal removed
            if m.group("id") in valid_ids:
                return m.group(0)
            removed += 1
            return ""

        new_part = ITEM_BLOCK.sub(repl, part)
        new_part = re.sub(r"\n{3,}", "\n\n", new_part)
        if not new_part.endswith("\n"):
            new_part += "\n"
        removed_total += removed
        if removed:
            per_cat[cat_id] = removed
        out.append(new_part)
    CATALOG.write_text("".join(out), encoding="utf-8")
    return removed_total, per_cat


def verify_counts(valid_ids: set[str]) -> None:
    text = CATALOG.read_text(encoding="utf-8")
    ids = ITEM_BLOCK.findall(text)  # returns id groups if only one group - fix
    ids = [m.group("id") for m in ITEM_BLOCK.finditer(text)]
    missing = [i for i in ids if i not in valid_ids]
    print(f"catalog items after: {len(ids)}; still-invalid: {len(missing)}")
    if missing[:20]:
        print("  sample invalid:", ", ".join(missing[:20]))


def main() -> None:
    stems = existing_prefab_stems()
    valid_ids = valid_catalog_ids(stems)
    print(f"disk prefabs={len(stems)} valid catalog ids={len(valid_ids)}")
    m_removed, m_kept = purge_manifest(stems)
    print(f"manifest removed={m_removed} kept={m_kept}")
    c_removed, per_cat = purge_catalog(valid_ids)
    print(f"catalog removed={c_removed}")
    for k, v in sorted(per_cat.items()):
        print(f"  {k}: -{v}")
    verify_counts(valid_ids)

    # Ensure enabled categories still have >= 6 items in catalog text
    text = CATALOG.read_text(encoding="utf-8")
    for part in re.split(r"(?=^  - id: )", text, flags=re.M)[1:]:
        cat_m = re.match(r"^  - id: (\S+)\n    label: (.*)\n    enabled: (\d+)", part)
        if not cat_m:
            continue
        cat_id, label, enabled = cat_m.group(1), cat_m.group(2), cat_m.group(3)
        count = len(list(ITEM_BLOCK.finditer(part)))
        enabled_count = len(re.findall(r"^      enabled: 1$", part, re.M))
        flag = "OK" if (enabled != "1" or enabled_count >= 6) else "TOO FEW"
        print(f"category {cat_id} enabled={enabled} items={count} enabledItems={enabled_count} {flag}")


if __name__ == "__main__":
    main()
