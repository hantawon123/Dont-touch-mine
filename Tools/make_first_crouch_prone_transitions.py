"""Bake matched Crouch <-> Prone transitions for the First 28-bone rig.

Blender --background --python Tools/make_first_crouch_prone_transitions.py --
    IDLE.fbx CROUCH.fbx PRONE_IDLE.fbx OUTPUT_DIRECTORY
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix


idle_path, crouch_path, prone_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def lerp_matrix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def pose_from_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True, ignore_leaf_bones=False)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    action = rig.animation_data.action
    frame = int(action.frame_range[0])
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    mesh = bpy.data.objects.get("Body")
    shapes = {}
    if mesh and mesh.data.shape_keys:
        shapes = {key.name: key.value for key in mesh.data.shape_keys.key_blocks[1:]}
    return ({bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}, shapes)


def export_fbx(rig, path, last):
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = last
    scene.frame_set(0)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [rig, *[o for o in scene.objects if o.type == "MESH"]]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=True,
        bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", armature_nodetype="NULL",
        primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
        mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)


def key_pose(rig, body, frame, source, target, t, source_shapes, target_shapes):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = lerp_matrix(source[bone.name], target[bone.name], t)
    bpy.context.view_layer.update()
    for bone in rig.pose.bones:
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)
    for key in body.data.shape_keys.key_blocks[1:]:
        key.value = source_shapes.get(key.name, 0.0) * (1.0 - t) + target_shapes.get(key.name, 0.0) * t
        key.keyframe_insert("value", frame=frame)


crouch_basis, crouch_shapes = pose_from_fbx(crouch_path)
prone_basis, prone_shapes = pose_from_fbx(prone_path)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=idle_path, use_anim=True, ignore_leaf_bones=False)
scene = bpy.context.scene
rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
body = bpy.data.objects["Body"]
assert set(crouch_basis) == set(prone_basis) == {bone.name for bone in rig.pose.bones}

# 1.2 seconds at 30fps: hands and knees flow together while all endpoints
# exactly match the existing Crouch_Idle and Prone_Idle clips.
last = 36
for name, source, target, source_shapes, target_shapes in (
    ("Crouch_To_Prone", crouch_basis, prone_basis, crouch_shapes, prone_shapes),
    ("Prone_To_Crouch", prone_basis, crouch_basis, prone_shapes, crouch_shapes),
):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    body.data.shape_keys.animation_data_clear()
    for frame in range(0, last + 1):
        t = smooth(frame / last)
        key_pose(rig, body, frame, source, target, t, source_shapes, target_shapes)
    export_fbx(rig, out_dir / f"FirstPlayerCapsule_{name}.fbx", last)
    print("EXPORTED", name)
