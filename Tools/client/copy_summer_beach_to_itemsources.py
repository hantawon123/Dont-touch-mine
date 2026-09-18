#!/usr/bin/env python3
"""Copy Summer Beach assets referenced by beach collection prefabs into ItemSources (keep .meta GUIDs)."""
from __future__ import annotations

import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
BEACH_PREFABS = ASSETS / "_Game/Content/ItemCollection/Prefabs/beach"
SRC_ROOT = ASSETS / "Summer Beach - Low Poly"
DST_ROOT = ASSETS / "ItemSources/Summer_Beach_Low_Poly"
GUID_RE = re.compile(r"guid:\s*([a-f0-9]{32})")


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
        asset = Path(str(meta)[:-5])  # strip .meta
        index[m.group(1)] = asset
    return index


def guids_in_file(path: Path) -> set[str]:
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return set()
    return set(GUID_RE.findall(text))


def collect_closure(seed_guids: set[str], index: dict[str, Path]) -> dict[str, Path]:
    """Return guid -> asset path for all deps under Summer Beach (and follow refs within pack)."""
    wanted: dict[str, Path] = {}
    queue = list(seed_guids)
    seen: set[str] = set()
    src_prefix = str(SRC_ROOT).replace("\\", "/")

    while queue:
        g = queue.pop()
        if g in seen:
            continue
        seen.add(g)
        asset = index.get(g)
        if asset is None:
            continue
        asset_s = str(asset).replace("\\", "/")
        # Only vendor assets from Summer Beach pack (not _Game materials etc.)
        if "/Summer Beach - Low Poly/" not in asset_s and not asset_s.endswith("/Summer Beach - Low Poly"):
            continue
        wanted[g] = asset
        # Follow refs inside asset + its .meta
        for path in (asset, Path(str(asset) + ".meta")):
            if path.is_file():
                for ng in guids_in_file(path):
                    if ng not in seen:
                        queue.append(ng)
        # If folder .meta somehow — skip
        # Also pull sibling files? FBX often needs materials folder via guid already.

    return wanted


def remap_path(src: Path) -> Path:
    rel = src.relative_to(SRC_ROOT)
    return DST_ROOT / rel


def copy_preserving_meta(wanted: dict[str, Path]) -> list[str]:
    logs: list[str] = []
    for g, src in sorted(wanted.items(), key=lambda kv: str(kv[1])):
        if not src.exists() and not src.is_dir():
            # Unity folders may exist as dirs without extension
            if not src.exists():
                logs.append(f"MISSING {g} {src}")
                continue
        dst = remap_path(src)
        dst.parent.mkdir(parents=True, exist_ok=True)
        src_meta = Path(str(src) + ".meta")
        dst_meta = Path(str(dst) + ".meta")

        if src.is_dir():
            dst.mkdir(parents=True, exist_ok=True)
        else:
            shutil.copy2(src, dst)

        if src_meta.is_file():
            shutil.copy2(src_meta, dst_meta)
            # ensure guid unchanged
            text = dst_meta.read_text(encoding="utf-8")
            if f"guid: {g}" not in text:
                logs.append(f"GUID MISMATCH {g} -> {dst_meta}")
        else:
            logs.append(f"NO META {src}")

        logs.append(f"OK {src.relative_to(ASSETS)} -> {dst.relative_to(ASSETS)}")
    return logs


def seed_from_beach_prefabs(index: dict[str, Path]) -> set[str]:
    seeds: set[str] = set()
    for prefab in BEACH_PREFABS.glob("*.prefab"):
        for g in guids_in_file(prefab):
            asset = index.get(g)
            if asset is None:
                continue
            if "Summer Beach - Low Poly" in str(asset).replace("\\", "/"):
                seeds.add(g)
    return seeds


def also_seed_from_item_list(index: dict[str, Path]) -> set[str]:
    """Ensure listed importer prefabs are included even if somehow unreferenced."""
    names = [
        "Beachball_color", "Beachball_dot", "Beachball_red",
        "Bucket_blue", "Bucket_yellow", "BucketCastle_green", "BucketCastle_red",
        "Cocktail_blue", "Cocktail_red", "Cocktail_white",
        "Coconut", "Coconut_cocktail",
        "Flipflop_black", "Flipflop_pink", "Flipflop_purple", "Flipflop_tropic",
        "RubberRing_medium", "Shovel_blue", "Shovel_orange", "Rake_green", "Rake_yellow",
        "Sunglasses", "Sunscreen",
        "SurfBoard_tinyBlack", "SurfBoard_tinyFluo", "SurfBoard_smallBlue", "SurfBoard_smallTurquoise",
        "Towel_blue", "Towel_pink", "Towel_red", "Towel_sea",
        "VolleyBall", "Watermelon", "Watermelon_quarter", "ParasolSmall",
    ]
    seeds: set[str] = set()
    prefab_dir = SRC_ROOT / "Prefabs"
    for name in names:
        p = prefab_dir / f"{name}.prefab"
        meta = Path(str(p) + ".meta")
        if meta.is_file():
            m = re.search(r"^guid:\s*([a-f0-9]{32})", meta.read_text(encoding="utf-8"), re.M)
            if m:
                seeds.add(m.group(1))
        # also parse prefab for deps
        if p.is_file():
            seeds |= guids_in_file(p)
    # filter to summer beach only later in closure
    return seeds


def write_folder_metas() -> None:
    """Ensure destination folder chain has .meta files (new guids OK for folders)."""
    # Unity will regenerate folder metas if missing; copy from source where possible
    for dirpath in [DST_ROOT, *DST_ROOT.rglob("*")]:
        if not dirpath.is_dir():
            continue
        rel = dirpath.relative_to(DST_ROOT)
        src_dir = SRC_ROOT / rel if rel.parts else SRC_ROOT
        src_meta = Path(str(src_dir) + ".meta")
        dst_meta = Path(str(dirpath) + ".meta")
        if src_meta.is_file() and not dst_meta.exists():
            shutil.copy2(src_meta, dst_meta)


def main() -> None:
    print("Indexing GUIDs...")
    index = build_guid_index()
    print(f"index size={len(index)}")

    seeds = seed_from_beach_prefabs(index) | {
        g for g in also_seed_from_item_list(index)
        if g in index and "Summer Beach - Low Poly" in str(index[g]).replace("\\", "/")
    }
    print(f"seed guids={len(seeds)}")

    wanted = collect_closure(seeds, index)
    print(f"closure assets={len(wanted)}")

    if DST_ROOT.exists():
        shutil.rmtree(DST_ROOT)
    DST_ROOT.mkdir(parents=True, exist_ok=True)

    # Root folder meta: new folder name needs its own meta; don't reuse Summer Beach root meta guid
    # to avoid duplicate guid with leftover original folder.
    logs = copy_preserving_meta(wanted)
    write_folder_metas()

    # Fix root: if we copied Summer Beach root meta onto DST incorrectly, skip
    root_meta = Path(str(DST_ROOT) + ".meta")
    src_root_meta = Path(str(SRC_ROOT) + ".meta")
    if root_meta.exists() and src_root_meta.exists():
        # Replace with unique guid for ItemSources folder
        import uuid
        new_guid = uuid.uuid4().hex
        text = src_root_meta.read_text(encoding="utf-8")
        text = re.sub(r"^guid:\s*[a-f0-9]{32}", f"guid: {new_guid}", text, count=1, flags=re.M)
        root_meta.write_text(text, encoding="utf-8")
        print(f"ItemSources root meta guid={new_guid}")

    out = ROOT / "Temp" / "summer_beach_itemsources_copy.log"
    out.parent.mkdir(exist_ok=True)
    out.write_text("\n".join(logs), encoding="utf-8")
    print(f"wrote {out} lines={len(logs)}")

    # Verify every seed guid exists under DST with same guid
    ok = 0
    bad = 0
    for g, src in wanted.items():
        dst = remap_path(src)
        meta = Path(str(dst) + ".meta")
        if meta.is_file() and f"guid: {g}" in meta.read_text(encoding="utf-8"):
            ok += 1
        else:
            bad += 1
            print(f"VERIFY FAIL {g} {dst}")
    print(f"verify ok={ok} bad={bad}")


if __name__ == "__main__":
    main()
