"""Export authored SmoothBear emote blends as Unity FBX clips.

blender --background --python Tools/export_smooth_bear_emote_clips.py

Exports every clip below. Pass `-- --only Emote_Wave` (repeatable) to re-export
just one; the dances are several hundred frames each and cost minutes otherwise.
"""
from pathlib import Path
import shutil
import sys
import bpy

GAME = Path(r"C:\Users\SSAFY\barleymilk\GameServer\ServerS15P21D205")
AUTHOR = Path(r"C:\Users\SSAFY\barleymilk\S15P21D205\source\blender\characters\SmoothBear\Emotes")
SOURCE_DIR = GAME / "source/blender/characters/SmoothBear/Emotes"
FBX_DIR = GAME / "Assets/_Game/Content/Characters/SmoothBear/EmoteSource"

CLIPS = (
    ("SmoothBear_Greeting.blend", "Greeting", "Emote_Wave"),
    ("SmoothBear_Taunt.blend", "Taunt", "Emote_Taunt"),
    ("SmoothBear_EmotePreview.blend", "Dance_Chicken", "Emote_Chicken"),
    ("SmoothBear_EmotePreview.blend", "Dance_HipHop", "Emote_HipHop"),
    ("SmoothBear_EmotePreview.blend", "Dance_SoulSpin", "Emote_Spin"),
)


def assign_action(rig, action):
    animation = rig.animation_data_create()
    animation.action = action
    if action.slots:
        animation.action_slot = action.slots[0]


def export_fbx(rig, path, first, last):
    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = last
    scene.render.fps = 30
    scene.frame_set(first)
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1,
        bake_anim_simplify_factor=0,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        armature_nodetype="NULL",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=False,
    )


def selected_clips():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    wanted = [argv[i + 1] for i, item in enumerate(argv) if item == "--only" and i + 1 < len(argv)]
    if not wanted:
        return CLIPS
    clips = tuple(clip for clip in CLIPS if clip[2] in wanted)
    missing = set(wanted) - {clip[2] for clip in clips}
    if missing:
        raise SystemExit(f"unknown clip(s): {sorted(missing)}")
    return clips


def main():
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    FBX_DIR.mkdir(parents=True, exist_ok=True)
    clips = selected_clips()
    copied = set()
    for filename, _, _ in clips:
        if filename in copied:
            continue
        src = AUTHOR / filename
        if not src.is_file():
            raise FileNotFoundError(src)
        shutil.copy2(src, SOURCE_DIR / filename)
        copied.add(filename)

    opened = None
    for filename, action_name, clip_name in clips:
        blend = SOURCE_DIR / filename
        if opened != filename:
            bpy.ops.wm.open_mainfile(filepath=str(blend), use_scripts=False)
            opened = filename
        rig = bpy.data.objects["DGN_Armature"]
        action = bpy.data.actions.get(action_name)
        if action is None:
            raise RuntimeError(f"{filename} has no action {action_name}")
        action.name = clip_name
        assign_action(rig, action)
        first, last = (int(value) for value in action.frame_range)
        dest = FBX_DIR / f"{clip_name}.fbx"
        export_fbx(rig, dest, first, last)
        print("EXPORTED", dest.name, action_name, first, last, flush=True)


if __name__ == "__main__":
    main()
