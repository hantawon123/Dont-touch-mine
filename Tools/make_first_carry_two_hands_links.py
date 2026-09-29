"""Carry_TwoHands posture links: crouch/prone transitions and put-up/down.

blender --background --python Tools/make_first_carry_two_hands_links.py -- FIRST_DIR
"""
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
PIT = "Armpit_Fill"
HOLD = FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx"

TRANSITIONS = (
    ("Carry_TwoHands_Crouch_Start", "Crouch_Start", "Carry_TwoHands", "Carry_TwoHands_Crouch_Idle", 24),
    ("Carry_TwoHands_Crouch_End", "Crouch_End", "Carry_TwoHands_Crouch_Idle", "Carry_TwoHands", 24),
    ("Carry_TwoHands_Prone_Start", "Prone_Start", "Carry_TwoHands", "Carry_TwoHands_Prone_Idle", 24),
    ("Carry_TwoHands_Prone_End", "Prone_End", "Carry_TwoHands_Prone_Idle", "Carry_TwoHands", 24),
    ("Carry_TwoHands_Crouch_To_Prone", "Crouch_To_Prone", "Carry_TwoHands_Crouch_Idle", "Carry_TwoHands_Prone_Idle", 36),
    ("Carry_TwoHands_Prone_To_Crouch", "Prone_To_Crouch", "Carry_TwoHands_Prone_Idle", "Carry_TwoHands_Crouch_Idle", 36),
)

PUTS = (
    ("PutUp_TwoHands_Crouch", "Crouch_Idle", "Carry_TwoHands_Crouch_Idle", True, 20),
    ("PutDown_TwoHands_Crouch", "Crouch_Idle", "Carry_TwoHands_Crouch_Idle", False, 20),
    ("PutUp_TwoHands_Prone", "Prone_Idle", "Carry_TwoHands_Prone_Idle", True, 20),
    ("PutDown_TwoHands_Prone", "Prone_Idle", "Carry_TwoHands_Prone_Idle", False, 20),
)


def descendants(bone):
    names = [bone.name]
    for child in bone.children:
        names.extend(descendants(child))
    return names


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=False)
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(
        a_loc.lerp(b_loc, t),
        a_rot.slerp(b_rot, t),
        a_scale.lerp(b_scale, t),
    )


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def pose_first(path):
    rig = load(path)
    first = int(rig.animation_data.action.frame_range[0])
    bpy.context.scene.frame_set(first)
    bpy.context.view_layer.update()
    body = bpy.data.objects.get("Body")
    shapes = {}
    if body is not None and body.data.shape_keys is not None:
        shapes = {key.name: key.value for key in body.data.shape_keys.key_blocks[1:]}
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}, shapes


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
    return frames


def write_meta(source_name, clip_name, last):
    source = FIRST / f"FirstPlayerCapsule_{source_name}.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{clip_name}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace(f"name: {source_name}", f"name: {clip_name}")
    text = text.replace("lastFrame: 60", f"lastFrame: {last}")
    text = text.replace("lastFrame: 48", f"lastFrame: {last}")
    text = text.replace("lastFrame: 36", f"lastFrame: {last}")
    dest.write_text(text, encoding="utf-8")
    print("META", clip_name, new_guid)


def export_fbx(path, last):
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 0
    scene.frame_end = last
    scene.frame_set(0)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
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


def apply_pit(body, deltas):
    if body.data.shape_keys is None:
        body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
    pit_key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
    if deltas and len(pit_key.data) == len(deltas):
        for index, delta in enumerate(deltas):
            pit_key.data[index].co = keys.key_blocks[0].data[index].co + Vector(delta)
    return keys


hold_rig = load(HOLD)
arm_bones = descendants(hold_rig.pose.bones["Shoulder.L"]) + descendants(hold_rig.pose.bones["Shoulder.R"])
arm_bones = list(dict.fromkeys(arm_bones))
hold_keys = bpy.data.objects["Body"].data.shape_keys
pit_deltas = []
if hold_keys is not None and PIT in hold_keys.key_blocks:
    pit_deltas = [
        hold_keys.key_blocks[PIT].data[i].co - hold_keys.key_blocks[0].data[i].co
        for i in range(len(hold_keys.key_blocks[0].data))
    ]

for clip_name, body_name, start_name, end_name, last in TRANSITIONS:
    body_frames = sample_clip(FIRST / f"FirstPlayerCapsule_{body_name}.fbx")
    start_pose, start_shapes = pose_first(FIRST / f"FirstPlayerCapsule_{start_name}.fbx")
    end_pose, end_shapes = pose_first(FIRST / f"FirstPlayerCapsule_{end_name}.fbx")
    rig = load(FIRST / f"FirstPlayerCapsule_{body_name}.fbx")
    keys = apply_pit(bpy.data.objects["Body"], pit_deltas)
    action = bpy.data.actions.new(clip_name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    body_len = len(body_frames)
    for frame in range(last + 1):
        amount = smooth(frame / last) if last > 0 else 1.0
        pose, body_shapes = body_frames[min(frame, body_len - 1)]
        for bone in ordered:
            bone.matrix_basis = pose[bone.name]
        bpy.context.view_layer.update()
        for name in arm_bones:
            if name in rig.pose.bones and name in start_pose and name in end_pose:
                rig.pose.bones[name].matrix_basis = mix(start_pose[name], end_pose[name], amount)
        bpy.context.view_layer.update()
        key_pose(rig, frame)
        for block in keys.key_blocks[1:]:
            start = start_shapes.get(block.name, 1.0 if block.name == PIT else body_shapes.get(block.name, 0.0))
            end = end_shapes.get(block.name, 1.0 if block.name == PIT else body_shapes.get(block.name, 0.0))
            block.value = start + (end - start) * amount
            block.keyframe_insert("value", frame=frame)
    export_fbx(FIRST / f"FirstPlayerCapsule_{clip_name}.fbx", last)
    write_meta(body_name, clip_name, last)
    print("BAKED", clip_name, last)

for clip_name, base_name, hold_name, pickup, last in PUTS:
    base_pose, base_shapes = pose_first(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    hold_pose, hold_shapes = pose_first(FIRST / f"FirstPlayerCapsule_{hold_name}.fbx")
    rig = load(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    keys = apply_pit(bpy.data.objects["Body"], pit_deltas)
    action = bpy.data.actions.new(clip_name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")
    blend_end = max(1, last - 4)
    for frame in range(last + 1):
        amount = smooth(min(1.0, frame / blend_end))
        progress = amount if pickup else 1.0 - amount
        for bone in rig.pose.bones:
            bone.rotation_mode = "QUATERNION"
            if bone.name in arm_bones and bone.name in hold_pose:
                bone.matrix_basis = mix(base_pose[bone.name], hold_pose[bone.name], progress)
            else:
                bone.matrix_basis = base_pose[bone.name]
        bpy.context.view_layer.update()
        key_pose(rig, frame)
        for block in keys.key_blocks[1:]:
            start = base_shapes.get(block.name, 0.0)
            end = hold_shapes.get(block.name, 1.0 if block.name == PIT else 0.0)
            block.value = start + (end - start) * progress
            block.keyframe_insert("value", frame=frame)
    export_fbx(FIRST / f"FirstPlayerCapsule_{clip_name}.fbx", last)
    write_meta(base_name, clip_name, last)
    print("BAKED", clip_name, last)

print("CARRY_TWO_HANDS_LINKS_DONE")
