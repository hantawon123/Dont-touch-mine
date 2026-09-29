"""Throw_TwoHands: Carry_TwoHands hold → both arms toss forward → idle.

blender --background --python Tools/make_first_throw_two_hands.py -- FIRST_DIR
"""
import math
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
IDLE_PATH = FIRST / "FirstPlayerCapsule_Idle.fbx"
HOLD_PATH = FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx"
CLIP = "Throw_TwoHands"
PIT = "Armpit_Fill"
LAST = 24
WIND = 10
RELEASE = 16
FINGERS = (
    "Finger_M1.L",
    "Finger_M2.L",
    "Finger_T1.L",
    "Finger_T2.L",
    "Finger_M1.R",
    "Finger_M2.R",
    "Finger_T1.R",
    "Finger_T2.R",
)

WIND_UPPER = {"L": Vector((0.40, 0.70, -0.52)), "R": Vector((-0.40, 0.70, -0.52))}
WIND_FOREARM = {"L": Vector((0.16, 0.90, -0.32)), "R": Vector((-0.16, 0.90, -0.32))}
WIND_HAND = {"L": Vector((0.10, 0.84, -0.26)), "R": Vector((-0.10, 0.84, -0.26))}
THROW_UPPER = {"L": Vector((0.26, 0.46, 0.85)), "R": Vector((-0.26, 0.46, 0.85))}
THROW_FOREARM = {"L": Vector((0.14, 0.22, 0.96)), "R": Vector((-0.14, 0.22, 0.96))}
THROW_HAND = {"L": Vector((0.10, 0.16, 0.98)), "R": Vector((-0.10, 0.16, 0.98))}


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


def twist(bone, radians):
    axis = (bone.tail - bone.head).normalized()
    loc, rot, _ = bone.matrix.decompose()
    bone.matrix = Matrix.LocRotScale(loc, Quaternion(axis, radians) @ rot, Vector((1, 1, 1)))
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


def write_meta():
    source = FIRST / "FirstPlayerCapsule_Throw.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{CLIP}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{CLIP}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace("name: Throw", f"name: {CLIP}")
    dest.write_text(text, encoding="utf-8")
    return new_guid


idle = pose_at(IDLE_PATH, 1)
hold = pose_at(HOLD_PATH, 1)

rig = load(IDLE_PATH)
body = bpy.data.objects["Body"]
hold_body = load(HOLD_PATH)
src_keys = bpy.data.objects["Body"].data.shape_keys
pit_deltas = []
if src_keys is not None and PIT in src_keys.key_blocks:
    pit_deltas = [
        src_keys.key_blocks[PIT].data[i].co - src_keys.key_blocks[0].data[i].co
        for i in range(len(src_keys.key_blocks[0].data))
    ]

rig = load(IDLE_PATH)
body = bpy.data.objects["Body"]
if body.data.shape_keys is None:
    body.shape_key_add(name="Basis")
keys = body.data.shape_keys
pit_key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
if pit_deltas and len(pit_key.data) == len(pit_deltas):
    for index, delta in enumerate(pit_deltas):
        pit_key.data[index].co = keys.key_blocks[0].data[index].co + delta

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
        bone.matrix_basis = idle_basis[bone.name]
    bpy.context.view_layer.update()

    spine = rig.pose.bones["Spine"]
    spine.rotation_mode = "XYZ"
    if frame <= WIND:
        coil = smooth(frame / WIND)
        spine.rotation_euler.x -= 0.16 * coil
        rig.pose.bones["Hips"].location.y -= 0.008 * coil
    elif frame <= RELEASE:
        t = smooth((frame - WIND) / (RELEASE - WIND))
        spine.rotation_euler.x += -0.16 + 0.22 * t
        rig.pose.bones["Hips"].location.y += -0.008 + 0.012 * t
    bpy.context.view_layer.update()

    for side, sign in (("L", 1.0), ("R", -1.0)):
        shoulder = rig.pose.bones[f"Shoulder.{side}"]
        shoulder.matrix_basis = mix_basis(
            idle_basis[shoulder.name],
            hold_basis[shoulder.name],
            carry,
        )
    bpy.context.view_layer.update()

    grip = 1.0 if frame <= RELEASE else 1.0 - smooth((frame - RELEASE) / 8.0)
    for name in FINGERS:
        if name in idle_basis and name in hold_basis:
            rig.pose.bones[name].matrix_basis = mix_basis(
                idle_basis[name],
                hold_basis[name],
                grip,
            )

    for side, sign in (("L", 1.0), ("R", -1.0)):
        upper_dir, lower_dir, hand_dir = dirs_at(frame, hold["dirs"][side], idle["dirs"][side], side)
        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        hand = rig.pose.bones[f"Hand.{side}"]
        aim(upper, upper_dir)
        aim(lower, lower_dir)
        aim(hand, hand_dir)
        twist(upper, sign * 0.12 * carry)
        twist(lower, -sign * 0.22 * carry)
        twist(hand, -sign * 0.95 * carry)
        elbow = (upper.tail - upper.head).angle(lower.tail - lower.head)
        if frame <= RELEASE and elbow < math.radians(10):
            raise RuntimeError(f"{side} arm too straight at {frame}: {math.degrees(elbow):.1f} deg")

    bpy.context.view_layer.update()
    key_pose(rig, frame)
    for block in keys.key_blocks[1:]:
        if block.name == PIT:
            block.value = carry
        elif block.name == "Belly_Breath":
            block.value = 0.0
        else:
            block.value = 0.0
        block.keyframe_insert("value", frame=frame)

scene = bpy.context.scene
scene.frame_start = 0
scene.frame_end = LAST
scene.render.fps = 30
for label, frame in (("START", 0), ("WIND", WIND), ("THROW", RELEASE), ("END", LAST)):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    for side in ("L", "R"):
        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        elbow = math.degrees((upper.tail - upper.head).angle(lower.tail - lower.head))
        print(
            label,
            side,
            frame,
            f"elbow={elbow:.1f}",
            "upper",
            tuple(round(v, 3) for v in (upper.tail - upper.head).normalized()),
        )

bpy.ops.object.select_all(action="DESELECT")
meshes = [obj for obj in scene.objects if obj.type == "MESH"]
for obj in [rig, *meshes]:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(FIRST / f"FirstPlayerCapsule_{CLIP}.fbx"),
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
guid = write_meta()
print("THROW_TWO_HANDS", FIRST / f"FirstPlayerCapsule_{CLIP}.fbx", guid)
