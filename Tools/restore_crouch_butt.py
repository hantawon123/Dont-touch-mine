"""Restore the original butt mesh by zeroing Crouch_Groin_Flat. Arms stay as-is.

Blender --background --python Tools/restore_crouch_butt.py -- INPUT.fbx OUTPUT.fbx
"""
import sys
from pathlib import Path

import bpy


input_path, output_path = sys.argv[sys.argv.index("--") + 1 :]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=input_path, use_anim=True, ignore_leaf_bones=False)

rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
body = bpy.data.objects["Body"]
keys = body.data.shape_keys
if keys:
    key = keys.key_blocks.get("Crouch_Groin_Flat")
    basis = keys.key_blocks[0]
    if key:
        for point, base in zip(key.data, basis.data):
            point.co = base.co.copy()

action = rig.animation_data.action
first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
scene = bpy.context.scene
scene.frame_start = first
scene.frame_end = last
scene.frame_set(first)
bpy.ops.object.select_all(action="DESELECT")
for obj in [rig, *[item for item in scene.objects if item.type == "MESH"]]:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
Path(output_path).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=output_path,
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
    embed_textures=True,
)
print("RESTORED_BUTT", input_path, "->", output_path)
