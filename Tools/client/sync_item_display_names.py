#!/usr/bin/env python3
"""Sync CarryableItem displayName on collection prefabs from ItemCatalog.asset."""
from __future__ import annotations

import codecs
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PREFAB_ROOT = ROOT / "Assets/_Game/Content/ItemCollection/Prefabs"
CATALOG = ROOT / "Assets/_Game/Content/Resources/Items/ItemCatalog.asset"

ITEM_BLOCK = re.compile(
    r"^    - id: (?P<id>\S+)\n"
    r"      displayName: (?P<name>.*?)\n"
    r"      enabled: \d+\n"
    r"      prefab: \{[^\}]*\}\n?",
    re.M,
)

DISPLAY_LINE = re.compile(r'^  displayName: (?P<val>.*)$', re.M)


def decode_unity_string(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith('"') and raw.endswith('"'):
        raw = raw[1:-1]
    try:
        return codecs.decode(raw, "unicode_escape")
    except Exception:
        return raw


def encode_unity_string(value: str) -> str:
    parts: list[str] = []
    for ch in value:
        o = ord(ch)
        if ch in ('\\', '"'):
            parts.append('\\' + ch)
        elif o < 0x20 or o > 0x7E:
            parts.append(f"\\u{o:04X}")
        else:
            parts.append(ch)
    return '"' + "".join(parts) + '"'


def load_catalog_names() -> dict[str, str]:
    text = CATALOG.read_text(encoding="utf-8")
    return {m.group("id"): decode_unity_string(m.group("name")) for m in ITEM_BLOCK.finditer(text)}


def prefab_for_catalog_id(cid: str) -> Path | None:
    if not cid.startswith("i") or len(cid) < 2:
        return None
    suffix = cid[1:]
    matches = list(PREFAB_ROOT.rglob(f"*_{suffix}.prefab"))
    return matches[0] if len(matches) == 1 else (matches[0] if matches else None)


def sync() -> None:
    names = load_catalog_names()
    updated = 0
    unchanged = 0
    missing = 0
    no_field = 0
    for cid, name in names.items():
        path = prefab_for_catalog_id(cid)
        if path is None:
            missing += 1
            continue
        text = path.read_text(encoding="utf-8")
        m = DISPLAY_LINE.search(text)
        if not m:
            no_field += 1
            continue
        current = decode_unity_string(m.group("val"))
        if current == name:
            unchanged += 1
            continue
        new_line = f"  displayName: {encode_unity_string(name)}"
        new_text = text[: m.start()] + new_line + text[m.end() :]
        path.write_text(new_text, encoding="utf-8")
        updated += 1
        print(f"  {path.name}: '{current}' -> '{name}'")
    print(f"updated={updated} unchanged={unchanged} missing={missing} no_field={no_field}")


if __name__ == "__main__":
    sync()
