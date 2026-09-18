#!/usr/bin/env python3
"""Migrate collection-prefab nested deps from local Asset Store packs into ItemSources."""
from __future__ import annotations

import re
import shutil
import uuid
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
PREFAB_ROOT = ASSETS / "_Game/Content/ItemCollection/Prefabs"
ITEMSOURCES = ASSETS / "ItemSources"
GUID_RE = re.compile(r"guid:\s*([a-f0-9]{32})")
SOURCE_PREFAB_RE = re.compile(
    r"m_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]{32})"
)

# Local pack roots that should live under ItemSources (team convention).
# Map: Assets/<pack> -> ItemSources/<folder_name>
PACK_MOVES = {
    "ithappy": "ithappy",
    "DestiaArt": "DestiaArt",
    "polyperfect": "polyperfect",
    "PolyWorkshop_BasementWorkshop": "PolyWorkshop_BasementWorkshop",
}


def build_guid_index() -> dict[str, Path]:
    index: dict[str, Path] = {}
    for meta in ASSETS.rglob("*.meta"):
        try:
            text = meta.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        m = re.search(r"^guid:\s*([a-f0-9]{32})", text, re.M)
        if not m:
            continue
        index[m.group(1)] = Path(str(meta)[:-5])
    return index


def bucket_for(path: Path) -> str:
    try:
        rel = path.relative_to(ASSETS).as_posix()
    except ValueError:
        return "OTHER"
    if rel.startswith("ItemSources/"):
        return "ItemSources"
    if rel.startswith("_Game/"):
        return "_Game"
    return rel.split("/")[0]


def guids_in_file(path: Path) -> set[str]:
    try:
        return set(GUID_RE.findall(path.read_text(encoding="utf-8", errors="ignore")))
    except OSError:
        return set()


def analyze(index: dict[str, Path]) -> dict[str, dict[str, list[str]]]:
    report: dict[str, dict[str, list[str]]] = {}
    for cat_dir in sorted(PREFAB_ROOT.iterdir()):
        if not cat_dir.is_dir():
            continue
        buckets: dict[str, list[str]] = defaultdict(list)
        for prefab in cat_dir.glob("*.prefab"):
            text = prefab.read_text(encoding="utf-8", errors="ignore")
            for g in SOURCE_PREFAB_RE.findall(text):
                path = index.get(g)
                if path is None or not path.exists():
                    buckets["MISSING"].append(g)
                else:
                    buckets[bucket_for(path)].append(path.relative_to(ASSETS).as_posix())
        report[cat_dir.name] = dict(buckets)
    return report


def collect_seeds_for_packs(
    index: dict[str, Path], packs: set[str]
) -> dict[str, set[str]]:
    """pack_name -> seed guids referenced by any collection prefab."""
    seeds: dict[str, set[str]] = {p: set() for p in packs}
    for cat_dir in PREFAB_ROOT.iterdir():
        if not cat_dir.is_dir():
            continue
        for prefab in cat_dir.glob("*.prefab"):
            text = prefab.read_text(encoding="utf-8", errors="ignore")
            for g in set(GUID_RE.findall(text)):
                path = index.get(g)
                if path is None:
                    continue
                b = bucket_for(path)
                if b in seeds:
                    seeds[b].add(g)
    return seeds


def collect_closure(
    seed_guids: set[str], index: dict[str, Path], pack_name: str
) -> dict[str, Path]:
    wanted: dict[str, Path] = {}
    queue = list(seed_guids)
    seen: set[str] = set()
    while queue:
        g = queue.pop()
        if g in seen:
            continue
        seen.add(g)
        asset = index.get(g)
        if asset is None:
            continue
        if bucket_for(asset) != pack_name:
            continue
        wanted[g] = asset
        for path in (asset, Path(str(asset) + ".meta")):
            if path.is_file():
                for ng in guids_in_file(path):
                    if ng not in seen:
                        queue.append(ng)
    return wanted


def copy_pack(
    pack_name: str, dst_name: str, wanted: dict[str, Path]
) -> tuple[int, int]:
    src_root = ASSETS / pack_name
    dst_root = ITEMSOURCES / dst_name
    if not src_root.is_dir():
        print(f"SKIP {pack_name}: source folder missing")
        return 0, 0

    # Merge into existing dst (don't wipe whole ithappy if partially present)
    ok = 0
    bad = 0
    for g, src in sorted(wanted.items(), key=lambda kv: str(kv[1])):
        try:
            rel = src.relative_to(src_root)
        except ValueError:
            print(f"OUTSIDE PACK {pack_name}: {src}")
            bad += 1
            continue
        dst = dst_root / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        src_meta = Path(str(src) + ".meta")
        dst_meta = Path(str(dst) + ".meta")

        if src.is_dir():
            dst.mkdir(parents=True, exist_ok=True)
        elif src.is_file():
            if not dst.exists():
                shutil.copy2(src, dst)
            # if exists, keep existing (assume same guid)
        else:
            print(f"MISSING FILE {src}")
            bad += 1
            continue

        if src_meta.is_file():
            if not dst_meta.exists():
                shutil.copy2(src_meta, dst_meta)
            text = dst_meta.read_text(encoding="utf-8")
            if f"guid: {g}" not in text:
                print(f"GUID MISMATCH {g} {dst_meta}")
                bad += 1
            else:
                ok += 1
        else:
            print(f"NO META {src}")
            bad += 1

        # Ensure parent folder metas exist (copy from source where possible)
        for parent in [dst, *dst.parents]:
            if parent == ITEMSOURCES or not str(parent).startswith(str(dst_root)):
                break
            if not parent.is_dir():
                continue
            pmeta = Path(str(parent) + ".meta")
            if pmeta.exists():
                continue
            try:
                sparent = src_root / parent.relative_to(dst_root)
            except ValueError:
                continue
            smeta = Path(str(sparent) + ".meta")
            if smeta.is_file():
                # folder metas: if copying would duplicate guid with still-present
                # original pack, assign new guid for intermediate folders only when
                # destination folder name path differs... For nested folders under
                # pack, keep guid and delete original pack later.
                shutil.copy2(smeta, pmeta)

    # Destination pack root meta: unique if new
    root_meta = Path(str(dst_root) + ".meta")
    src_meta = Path(str(src_root) + ".meta")
    if not root_meta.exists():
        new_guid = uuid.uuid4().hex
        if src_meta.is_file():
            text = src_meta.read_text(encoding="utf-8")
            text = re.sub(
                r"^guid:\s*[a-f0-9]{32}", f"guid: {new_guid}", text, count=1, flags=re.M
            )
            root_meta.write_text(text, encoding="utf-8")
        else:
            root_meta.write_text(
                f"fileFormatVersion: 2\nguid: {new_guid}\nfolderAsset: yes\n"
                "DefaultImporter:\n  externalObjects: {}\n  userData: \n"
                "  assetBundleName: \n  assetBundleVariant: \n",
                encoding="utf-8",
            )
    return ok, bad


def rewrite_manifest_sources(pack_moves: dict[str, str]) -> int:
    manifest = ASSETS / "_Game/Content/ItemCollection/ItemCollection.json"
    text = manifest.read_text(encoding="utf-8")
    total = 0
    for pack, dst in pack_moves.items():
        old = f"Assets/{pack}/"
        new = f"Assets/ItemSources/{dst}/"
        c = text.count(old)
        if c:
            text = text.replace(old, new)
            total += c
            print(f"JSON rewrite {pack}: {c}")
    manifest.write_text(text, encoding="utf-8")
    return total


def remove_original_pack(pack_name: str) -> None:
    src = ASSETS / pack_name
    meta = Path(str(src) + ".meta")
    if src.exists():
        shutil.rmtree(src)
        print(f"removed {src}")
    if meta.exists():
        meta.unlink()
        print(f"removed {meta}")


def verify(index_rebuild: bool = True) -> None:
    index = build_guid_index() if index_rebuild else {}
    # quick verify nested sources
    idx = build_guid_index()
    for cat_dir in sorted(PREFAB_ROOT.iterdir()):
        if not cat_dir.is_dir():
            continue
        bad = 0
        ok_item = 0
        external = defaultdict(int)
        for prefab in cat_dir.glob("*.prefab"):
            text = prefab.read_text(encoding="utf-8", errors="ignore")
            for g in SOURCE_PREFAB_RE.findall(text):
                path = idx.get(g)
                if path is None or not path.exists():
                    bad += 1
                else:
                    b = bucket_for(path)
                    if b == "ItemSources":
                        ok_item += 1
                    elif b == "_Game":
                        ok_item += 1
                    else:
                        external[b] += 1
        print(
            f"VERIFY {cat_dir.name}: item/_Game={ok_item} missing={bad} external={dict(external)}"
        )


def main() -> None:
    print("Indexing GUIDs...")
    index = build_guid_index()
    print(f"index={len(index)}")

    print("\n=== BEFORE ===")
    for cat, buckets in analyze(index).items():
        ext = {
            k: len(v)
            for k, v in buckets.items()
            if k not in ("ItemSources", "_Game")
        }
        print(f"{cat}: {ext or 'OK'}")

    seeds = collect_seeds_for_packs(index, set(PACK_MOVES))
    for pack, guids in seeds.items():
        print(f"seeds {pack}: {len(guids)}")

    for pack, dst in PACK_MOVES.items():
        if not seeds.get(pack):
            print(f"No seeds for {pack}, skip copy")
            continue
        wanted = collect_closure(seeds[pack], index, pack)
        print(f"closure {pack}: {len(wanted)}")
        ok, bad = copy_pack(pack, dst, wanted)
        print(f"copied {pack} -> ItemSources/{dst}: ok={ok} bad={bad}")

    rewrite_manifest_sources(PACK_MOVES)

    # Remove originals to avoid duplicate GUIDs
    for pack in PACK_MOVES:
        if (ASSETS / pack).exists():
            remove_original_pack(pack)

    print("\n=== AFTER ===")
    verify()


if __name__ == "__main__":
    main()
