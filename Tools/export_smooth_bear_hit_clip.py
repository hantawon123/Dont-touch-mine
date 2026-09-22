"""Export the authored SmoothBear Hit action as a Unity FBX clip.

blender --background --python Tools/export_smooth_bear_hit_clip.py

Reads source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend (built by
Tools/build_smooth_bear_hit.py and Tools/build_smooth_bear_stun_start.py) and writes
Assets/.../SmoothBear/CombatSource/<clip>.fbx for every clip below. Pass `-- --only Hit`
(repeatable) to export a subset. SmoothBearEmoteClipImporter then replaces the matching
Animations/<clip>.anim in place; for Hit it also re-bakes the posture variants through
CarryTwoHandsHitClipBaker.
"""
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from export_smooth_bear_emote_clips import assign_action, export_fbx  # noqa: E402

BLEND = ROOT / "source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend"
FBX_DIR = ROOT / "Assets/_Game/Content/Characters/SmoothBear/CombatSource"
CLIPS = ("Hit", "Stun_Start")


def selected_clips():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wanted = [argv[i + 1] for i, item in enumerate(argv) if item == "--only" and i + 1 < len(argv)]
    if not wanted:
        return CLIPS
    missing = set(wanted) - set(CLIPS)
    if missing:
        raise SystemExit(f"unknown clip(s): {sorted(missing)}")
    return tuple(clip for clip in CLIPS if clip in wanted)


def main():
    FBX_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(BLEND), use_scripts=False)
    rig = bpy.data.objects["DGN_Armature"]
    for name in selected_clips():
        action = bpy.data.actions.get(name)
        if action is None:
            raise RuntimeError(f"{BLEND.name} has no action {name}")
        assign_action(rig, action)
        first, last = (int(value) for value in action.frame_range)
        dest = FBX_DIR / f"{name}.fbx"
        export_fbx(rig, dest, first, last)
        print("EXPORTED", dest, first, last, flush=True)


if __name__ == "__main__":
    main()
