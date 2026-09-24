"""Inspect SmoothBear emote blend files."""
import bpy
from pathlib import Path

ROOTS = [
    Path(r"C:\Users\SSAFY\barleymilk\S15P21D205\source\blender\characters\SmoothBear\Emotes"),
    Path(r"C:\Users\SSAFY\barleymilk\GameServer\ServerS15P21D205\source\blender\characters\SmoothBear\Emotes"),
]
files = [
    "SmoothBear_Greeting.blend",
    "SmoothBear_Taunt.blend",
    "SmoothBear_Insult.blend",
]

for root in ROOTS:
    print("ROOT", root, "exists", root.is_dir())

root = next(path for path in ROOTS if (path / files[0]).is_file())
print("USING", root)

for name in files:
    path = root / name
    bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    print("FILE", name)
    print("  objects", [f"{obj.name}:{obj.type}" for obj in bpy.data.objects if obj.type in ("ARMATURE", "MESH")][:20])
    for action in bpy.data.actions:
        users = action.users
        fake = action.use_fake_user
        first, last = action.frame_range
        print(f"  ACTION {action.name!r} users={users} fake={fake} frames={int(first)}-{int(last)}")
    rig = bpy.data.objects.get("DGN_Armature")
    if rig is not None and rig.animation_data and rig.animation_data.action:
        print("  RIG_ACTION", rig.animation_data.action.name)
    print(flush=True)
