"""Throw_TwoHands from prone carry: start on hold, mild wind, toss, settle to Prone_Idle.

Also overlays that throw onto crawl so throwing while crawling keeps the legs.

blender --background --python Tools/make_first_throw_two_hands_prone.py -- FIRST_DIR
"""
import math
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
PRONE = FIRST / "FirstPlayerCapsule_Prone_Idle.fbx"
HOLD = FIRST / "FirstPlayerCapsule_Carry_TwoHands_Prone_Idle.fbx"
CLIP = "Throw_TwoHands_Prone"
CRAWL_CLIP = "Throw_TwoHands_Crawl"
PIT = "Armpit_Fill"
LAST = 24
WIND = 8
RELEASE = 16
FINGERS = (
    "Finger_M1.L", "Finger_M2.L", "Finger_T1.L", "Finger_T2.L",
    "Finger_M1.R", "Finger_M2.R", "Finger_T1.R", "Finger_T2.R",
)
ARM_WEIGHTS = {
    "Shoulder.L": 1.0, "UpperArm.L": 1.0, "Arm.L": 1.0, "Hand.L": 1.0,
    "Finger_M1.L": 1.0, "Finger_M2.L": 1.0, "Finger_T1.L": 1.0, "Finger_T2.L": 1.0,
    "Shoulder.R": 1.0, "UpperArm.R": 1.0, "Arm.R": 1.0, "Hand.R": 1.0,
    "Finger_M1.R": 1.0, "Finger_M2.R": 1.0, "Finger_T1.R": 1.0, "Finger_T2.R": 1.0,
    "Spine": 0.35, "Neck": 0.20,
}

# Hold is ~(+X, +Y small, +Z head). Wind only a little back/up, then throw along +Z.
WIND_UPPER = {"L": Vector((0.48, 0.38, 0.55)), "R": Vector((-0.48, 0.38, 0.55))}
WIND_FOREARM = {"L": Vector((0.18, 0.90, 0.28)), "R": Vector((-0.18, 0.90, 0.28))}
WIND_HAND = {"L": Vector((0.16, 0.88, 0.32)), "R": Vector((-0.16, 0.88, 0.32))}
THROW_UPPER = {"L": Vector((0.28, 0.20, 0.94)), "R": Vector((-0.28, 0.20, 0.94))}
THROW_FOREARM = {"L": Vector((0.14, 0.12, 0.98)), "R": Vector((-0.14, 0.12, 0.98))}
THROW_HAND = {"L": Vector((0.12, 0.10, 0.99)), "R": Vector((-0.12, 0.10, 0.99))}


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def nlerp(a, b, t):
    mixed = a.lerp(b, t)
    if mixed.length < 1e-8:
        return b.normalized()
    return mixed.normalized()


def mix_basis(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(
        a_loc.lerp(b_loc, t),
        a_rot.slerp(b_rot, t),
        a_scale.lerp(b_scale, t),
    )


def aim(bone, direction):
    current = bone.tail - bone.head
    if current.length < 1e-8 or direction.length < 1e-8:
        return
    rotation = current.normalized().rotation_difference(direction.normalized())
    loc, rot, _ = bone.matrix.decompose()
    bone.matrix = Matrix.LocRotScale(loc, rotation @ rot, Vector((1, 1, 1)))
    bpy.context.view_layer.update()


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=False)
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_at(path, frame):
    rig = load(path)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    dirs = {}
    for side in ("L", "R"):
        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        hand = rig.pose.bones[f"Hand.{side}"]
        dirs[side] = {
            "upper": (upper.tail - upper.head).normalized(),
            "lower": (lower.tail - lower.head).normalized(),
            "hand": (hand.tail - hand.head).normalized(),
        }
    return {
        "basis": {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones},
        "dirs": dirs,
    }


def dirs_at(frame, hold_dirs, idle_dirs, side):
    keys = (
        (0, hold_dirs["upper"], hold_dirs["lower"], hold_dirs["hand"]),
        (WIND, WIND_UPPER[side], WIND_FOREARM[side], WIND_HAND[side]),
        (RELEASE, THROW_UPPER[side], THROW_FOREARM[side], THROW_HAND[side]),
    )
    if frame >= RELEASE:
        settle = smooth((frame - RELEASE) / (LAST - RELEASE))
        return (
            nlerp(THROW_UPPER[side], idle_dirs["upper"], settle),
            nlerp(THROW_FOREARM[side], idle_dirs["lower"], settle),
            nlerp(THROW_HAND[side], idle_dirs["hand"], settle),
        )
    if frame <= keys[0][0]:
        return keys[0][1].copy(), keys[0][2].copy(), keys[0][3].copy()
    for index, (key_frame, upper, lower, hand) in enumerate(keys[:-1]):
        next_frame, next_upper, next_lower, next_hand = keys[index + 1]
        if frame <= next_frame:
            t = smooth((frame - key_frame) / (next_frame - key_frame))
            return nlerp(upper, next_upper, t), nlerp(lower, next_lower, t), nlerp(hand, next_hand, t)
    return keys[-1][1].copy(), keys[-1][2].copy(), keys[-1][3].copy()


def write_meta(clip_name):
    source = FIRST / "FirstPlayerCapsule_Throw_TwoHands.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{clip_name}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace("name: Throw_TwoHands", f"name: {clip_name}")
    dest.write_text(text, encoding="utf-8")
    print("META", clip_name, new_guid)
    return new_guid


def export_fbx(path, last):
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = last
    scene.render.fps = 30
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


idle = pose_at(PRONE, 1)
hold = pose_at(HOLD, 1)
rig = load(PRONE)
body = bpy.data.objects["Body"]
if body.data.shape_keys is None:
    body.shape_key_add(name="Basis")
keys = body.data.shape_keys
clip = bpy.data.actions.new(CLIP)
clip.use_fake_user = True
rig.animation_data_create().action = clip
if clip.slots:
    rig.animation_data.action_slot = clip.slots[0]
keys.animation_data_clear()
keys.animation_data_create()
keys.animation_data.action = bpy.data.actions.new(f"{CLIP}_shapes")

ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
idle_basis = idle["basis"]
hold_basis = hold["basis"]

for frame in range(LAST + 1):
    carry = 1.0 - (smooth((frame - RELEASE) / (LAST - RELEASE)) if frame > RELEASE else 0.0)
    for bone in ordered:
        bone.matrix_basis = mix_basis(idle_basis[bone.name], hold_basis[bone.name], carry)
    bpy.context.view_layer.update()

    grip = 1.0 if frame <= RELEASE else 1.0 - smooth((frame - RELEASE) / 8.0)
    for name in FINGERS:
        if name in idle_basis and name in hold_basis:
            rig.pose.bones[name].matrix_basis = mix_basis(
                idle_basis[name],
                hold_basis[name],
                grip,
            )

    for side in ("L", "R"):
        upper_dir, lower_dir, hand_dir = dirs_at(frame, hold["dirs"][side], idle["dirs"][side], side)
        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        hand = rig.pose.bones[f"Hand.{side}"]
        aim(upper, upper_dir)
        aim(lower, lower_dir)
        aim(hand, hand_dir)

    bpy.context.view_layer.update()
    key_pose(rig, frame)
    for block in keys.key_blocks[1:]:
        block.value = 0.0
        block.keyframe_insert("value", frame=frame)

scene = bpy.context.scene
for label, frame in (("START", 0), ("WIND", WIND), ("THROW", RELEASE), ("END", LAST)):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    for side in ("L", "R"):
        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        elbow = math.degrees((upper.tail - upper.head).angle(lower.tail - lower.head))
        print(label, side, frame, f"elbow={elbow:.1f}",
              "upper", tuple(round(v, 3) for v in (upper.tail - upper.head).normalized()))

export_fbx(FIRST / f"FirstPlayerCapsule_{CLIP}.fbx", LAST)
write_meta(CLIP)
print("BAKED", CLIP)

source = sample_clip(FIRST / f"FirstPlayerCapsule_{CLIP}.fbx")
base = sample_clip(FIRST / "FirstPlayerCapsule_Carry_TwoHands_Crawl_Forward.fbx")
source_len = len(source)
base_len = len(base)
rig = load(PRONE)
body = bpy.data.objects["Body"]
keys = body.data.shape_keys
action = bpy.data.actions.new(CRAWL_CLIP)
action.use_fake_user = True
rig.animation_data_create().action = action
if action.slots:
    rig.animation_data.action_slot = action.slots[0]
if keys is not None:
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{CRAWL_CLIP}_shapes")
ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
for frame in range(LAST + 1):
    source_pose, source_shapes = source[min(frame, source_len - 1)]
    base_pose, base_shapes = base[frame % base_len]
    for bone in ordered:
        weight = ARM_WEIGHTS.get(bone.name, 0.0)
        if weight > 0.0 and bone.name in source_pose:
            bone.matrix_basis = mix_basis(base_pose[bone.name], source_pose[bone.name], weight)
        else:
            bone.matrix_basis = base_pose[bone.name]
    bpy.context.view_layer.update()
    key_pose(rig, frame)
    if keys is not None:
        for key in keys.key_blocks[1:]:
            key.value = base_shapes.get(key.name, 0.0)
            key.keyframe_insert("value", frame=frame)
export_fbx(FIRST / f"FirstPlayerCapsule_{CRAWL_CLIP}.fbx", LAST)
write_meta(CRAWL_CLIP)
print("BAKED", CRAWL_CLIP)
print("THROW_TWO_HANDS_PRONE_DONE")
