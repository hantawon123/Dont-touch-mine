"""Idle → Carry_TwoHands raise (PutUp) and the reverse PutDown.

blender --background --python Tools/make_first_putup_two_hands.py -- FIRST_DIR
"""
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
LAST = 20
BLEND_END = 16
PIT = "Armpit_Fill"
ARM_WEIGHTS = {
    "Shoulder.L": 1.0,
    "UpperArm.L": 1.0,
    "Arm.L": 1.0,
    "Hand.L": 1.0,
    "Finger_M1.L": 1.0,
    "Finger_M2.L": 1.0,
    "Finger_T1.L": 1.0,
    "Finger_T2.L": 1.0,
    "Shoulder.R": 1.0,
    "UpperArm.R": 1.0,
    "Arm.R": 1.0,
    "Hand.R": 1.0,
    "Finger_M1.R": 1.0,
    "Finger_M2.R": 1.0,
    "Finger_T1.R": 1.0,
    "Finger_T2.R": 1.0,
    "Spine": 0.18,
    "Neck": 0.10,
}


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_at(rig, frame):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def shapes_at(path):
    rig = load(path)
    first = int(rig.animation_data.action.frame_range[0])
    bpy.context.scene.frame_set(first)
    bpy.context.view_layer.update()
    body = bpy.data.objects["Body"]
    if body.data.shape_keys is None:
        return {}
    return {key.name: key.value for key in body.data.shape_keys.key_blocks[1:]}


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def write_meta(source_name, clip_name):
    source = FIRST / f"FirstPlayerCapsule_{source_name}.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{clip_name}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace(f"name: {source_name}", f"name: {clip_name}")
    text = text.replace("lastFrame: 60", f"lastFrame: {LAST}")
    dest.write_text(text, encoding="utf-8")


def copy_pit(from_path, to_body):
    load(from_path)
    src = bpy.data.objects["Body"].data.shape_keys
    deltas = [
        src.key_blocks[PIT].data[i].co - src.key_blocks[0].data[i].co
        for i in range(len(src.key_blocks[0].data))
    ]
    return deltas


idle = pose_at(load(FIRST / "FirstPlayerCapsule_Idle.fbx"), 1)
idle_shapes = shapes_at(FIRST / "FirstPlayerCapsule_Idle.fbx")
hold = pose_at(load(FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx"), 1)
hold_shapes = shapes_at(FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx")
pit_deltas = copy_pit(FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx", None)

for clip_name, pickup, meta_src in (
    ("PutUp_TwoHands", True, "Pickup_Low"),
    ("PutDown_TwoHands", False, "PutDown_Low"),
):
    rig = load(FIRST / "FirstPlayerCapsule_Idle.fbx")
    body = bpy.data.objects["Body"]
    if body.data.shape_keys is None:
        body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
    pit_key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
    if len(pit_key.data) == len(pit_deltas):
        for index, delta in enumerate(pit_deltas):
            pit_key.data[index].co = keys.key_blocks[0].data[index].co + Vector(delta)

    rig.animation_data_create().action = bpy.data.actions.new(clip_name)
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")

    for frame in range(LAST + 1):
        amount = smooth(min(1.0, frame / BLEND_END))
        progress = amount if pickup else 1.0 - amount
        for bone in rig.pose.bones:
            bone.rotation_mode = "QUATERNION"
            weight = ARM_WEIGHTS.get(bone.name, 0.0) * progress
            if weight > 0.0:
                bone.matrix_basis = mix(idle[bone.name], hold[bone.name], weight)
            else:
                bone.matrix_basis = idle[bone.name]
        bpy.context.view_layer.update()
        for bone in rig.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(prop, frame=frame)
        for block in keys.key_blocks[1:]:
            start = idle_shapes.get(block.name, 0.0)
            end = hold_shapes.get(block.name, 1.0 if block.name == PIT else 0.0)
            block.value = start + (end - start) * progress
            block.keyframe_insert("value", frame=frame)

    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = LAST
    scene.render.fps = 30
    scene.frame_set(0)
    bpy.ops.export_scene.fbx(
        filepath=str(FIRST / f"FirstPlayerCapsule_{clip_name}.fbx"),
        use_selection=False,
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
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )
    write_meta(meta_src, clip_name)
    print("BAKED", clip_name, LAST)
