"""Fix First crouch FBX arms to a forward ㄴ bend and soften the groin key.

Blender --background --python Tools/fix_first_crouch_pose.py -- INPUT.fbx OUTPUT.fbx MODE
"""
import sys
from pathlib import Path

import bpy
from mathutils import Vector


input_path, output_path, mode = sys.argv[sys.argv.index("--") + 1 :]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=input_path, use_anim=True, ignore_leaf_bones=False)

rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
body = bpy.data.objects["Body"]
action = rig.animation_data.action
first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
scene = bpy.context.scene


def crouch_weight(frame):
    span = max(1, last - first)
    progress = (frame - first) / span
    if mode == "stand_to_crouch":
        return progress
    if mode == "crouch_to_stand":
        return 1.0 - progress
    return 1.0


def rebuild_groin_key():
    keys = body.data.shape_keys
    if not keys:
        return
    key = keys.key_blocks.get("Crouch_Groin_Flat")
    if not key:
        return
    basis = keys.key_blocks[0]
    for point, base in zip(key.data, basis.data):
        x, y, z = base.co
        point.co = base.co.copy()
        centre = max(0.0, 1.0 - abs(x) / 0.10) ** 2
        height = max(0.0, 1.0 - abs(y - 0.45) / 0.12) ** 2
        depth = max(0.0, 1.0 - abs(z) / 0.07) ** 2
        weight = centre * height * depth
        if weight > 0:
            point.co.y += 0.10 * weight


def apply_world_rotation(bone, world_rot):
    axes = bone.matrix.to_3x3()
    local = axes.inverted() @ world_rot.to_matrix() @ axes
    bone.matrix_basis = bone.matrix_basis @ local.to_4x4()


def aim_bone(bone, desired_dir, weight):
    if weight <= 0:
        return
    current = bone.tail - bone.head
    if current.length < 1e-8:
        return
    blended = current.normalized().lerp(desired_dir.normalized(), weight)
    if blended.length < 1e-8:
        return
    apply_world_rotation(bone, current.normalized().rotation_difference(blended.normalized()))


desired_upper = {
    "UpperArm.L": Vector((0.20, -0.94, 0.28)),
    "UpperArm.R": Vector((-0.20, -0.94, 0.28)),
}
desired_forearm = {
    "Arm.L": Vector((0.08, -0.12, 0.99)),
    "Arm.R": Vector((-0.08, -0.12, 0.99)),
}

rebuild_groin_key()
for bone in rig.pose.bones:
    bone.rotation_mode = "QUATERNION"

for frame in range(first, last + 1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    weight = crouch_weight(frame)
    for name, direction in desired_upper.items():
        aim_bone(rig.pose.bones[name], direction, weight)
        bpy.context.view_layer.update()
    for name, direction in desired_forearm.items():
        aim_bone(rig.pose.bones[name], direction, weight)
        bpy.context.view_layer.update()
    for name in (*desired_upper, *desired_forearm):
        bone = rig.pose.bones[name]
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)

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
print("FIXED", input_path, "->", output_path, "mode", mode, "frames", first, last)
