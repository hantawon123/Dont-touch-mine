"""Overlay Throw_TwoHands onto carry walk / run / crouch bases.

blender --background --python Tools/make_first_throw_two_hands_locomotion.py -- FIRST_DIR
"""
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
THROW = "Throw_TwoHands"
PIT = "Armpit_Fill"
LAST = 24
WEIGHTS = {
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
    "Spine": 0.55,
    "Neck": 0.28,
}
BASES = (
    ("Walk", "Carry_TwoHands_Walk_Forward"),
    ("Run", "Carry_TwoHands_Run_Forward"),
    ("Crouch", "Carry_TwoHands_Crouch_Idle"),
    ("Crouch_Walk", "Carry_TwoHands_Crouch_Walk_Forward"),
)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=False)
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def sample_clip(path):
    rig = load(path)
    action = rig.animation_data.action
    first, last = int(action.frame_range[0]), int(action.frame_range[1])
    body = bpy.data.objects.get("Body")
    keys = body.data.shape_keys if body is not None else None
    frames = []
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
        shapes = {}
        if keys is not None:
            shapes = {key.name: key.value for key in keys.key_blocks[1:]}
        frames.append((pose, shapes))
    pit_deltas = []
    if keys is not None and PIT in keys.key_blocks:
        basis = keys.key_blocks[0]
        pit = keys.key_blocks[PIT]
        pit_deltas = [pit.data[i].co - basis.data[i].co for i in range(len(basis.data))]
    return frames, pit_deltas


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(
        a_loc.lerp(b_loc, t),
        a_rot.slerp(b_rot, t),
        a_scale.lerp(b_scale, t),
    )


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def write_meta(clip_name):
    source = FIRST / f"FirstPlayerCapsule_{THROW}.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{clip_name}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace(f"name: {THROW}", f"name: {clip_name}")
    dest.write_text(text, encoding="utf-8")
    print("META", clip_name, new_guid)


source, pit_deltas = sample_clip(FIRST / f"FirstPlayerCapsule_{THROW}.fbx")
source_len = len(source)

for suffix, base_name in BASES:
    clip_name = f"{THROW}_{suffix}"
    base, _ = sample_clip(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    base_len = len(base)
    rig = load(FIRST / "FirstPlayerCapsule_Idle.fbx")
    body = bpy.data.objects["Body"]
    if body.data.shape_keys is None:
        body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
    pit_key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
    if pit_deltas and len(pit_key.data) == len(pit_deltas):
        for index, delta in enumerate(pit_deltas):
            pit_key.data[index].co = keys.key_blocks[0].data[index].co + Vector(delta)

    action = bpy.data.actions.new(clip_name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")

    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    for frame in range(LAST + 1):
        source_pose, source_shapes = source[min(frame, source_len - 1)]
        base_pose, base_shapes = base[frame % base_len]
        for bone in ordered:
            name = bone.name
            weight = WEIGHTS.get(name, 0.0)
            if weight > 0.0 and name in source_pose:
                bone.matrix_basis = mix(base_pose[name], source_pose[name], weight)
            else:
                bone.matrix_basis = base_pose[name]
        bpy.context.view_layer.update()
        key_pose(rig, frame)
        for key in keys.key_blocks[1:]:
            if key.name == PIT:
                key.value = source_shapes.get(PIT, 1.0)
            else:
                key.value = base_shapes.get(key.name, 0.0)
            key.keyframe_insert("value", frame=frame)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 0
    scene.frame_end = LAST
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
    write_meta(clip_name)
    print("EXPORTED", clip_name)

print("THROW_TWO_HANDS_LOCOMOTION_DONE")
