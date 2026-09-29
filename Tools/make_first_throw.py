"""Build Throw: carry, open upper arm to the side, raise both segments,
straighten into a line to throw, then return to idle.

Blender --background --python Tools/make_first_throw.py -- IDLE.fbx CARRY.fbx OUT_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


idle_path, carry_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)

FIRST = 1
LAST = 41
RELEASE = 26
FINGERS_R = ("Finger_M1.R", "Finger_M2.R", "Finger_T1.R", "Finger_T2.R")
SIDE = 10
LIFT = 18
# Right arm, character faces +Z. Upper arm opens on -X, then both rise,
# then upper and lower share one forward line.
SIDE_UPPER = Vector((-0.96, -0.12, 0.24))
SIDE_LOWER = Vector((-0.42, -0.18, 0.89))
LIFT_UPPER = Vector((-0.72, 0.58, 0.38))
LIFT_LOWER = Vector((-0.52, 0.62, 0.58))
THROW_DIR = Vector((-0.16, 0.22, 0.96))


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def nlerp(a, b, t):
    mixed = a.lerp(b, t)
    if mixed.length < 1e-8:
        return b.normalized()
    return mixed.normalized()


def aim(bone, direction):
    current = bone.tail - bone.head
    if current.length < 1e-8 or direction.length < 1e-8:
        return
    rotation = current.normalized().rotation_difference(direction.normalized())
    bone.matrix = Matrix.LocRotScale(
        bone.head,
        rotation @ bone.matrix.to_quaternion(),
        bone.matrix.to_scale(),
    )
    bpy.context.view_layer.update()


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def export_fbx(rig, path, first, last):
    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = last
    scene.frame_set(first)
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    for obj in [rig, *meshes]:
        obj.hide_set(False)
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
        embed_textures=True,
    )
    print("EXPORTED", path, first, last)


def sample_pose(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=False)
    scene = bpy.context.scene
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    first = int(rig.animation_data.action.frame_range[0])
    scene.frame_set(first)
    bpy.context.view_layer.update()
    upper = rig.pose.bones["UpperArm.R"]
    lower = rig.pose.bones["Arm.R"]
    return {
        "basis": {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones},
        "hand_q": rig.pose.bones["Hand.R"].matrix.to_quaternion().copy(),
        "upper": (upper.tail - upper.head).normalized(),
        "lower": (lower.tail - lower.head).normalized(),
    }


def dirs_at(frame, carry_pose, idle_pose):
    keys = (
        (FIRST, carry_pose["upper"], carry_pose["lower"]),
        (SIDE, SIDE_UPPER, SIDE_LOWER),
        (LIFT, LIFT_UPPER, LIFT_LOWER),
        (RELEASE, THROW_DIR, THROW_DIR),
    )
    if frame >= RELEASE:
        settle = smooth((frame - RELEASE) / (LAST - RELEASE))
        return nlerp(THROW_DIR, idle_pose["upper"], settle), nlerp(THROW_DIR, idle_pose["lower"], settle)
    if frame <= keys[0][0]:
        return keys[0][1].copy(), keys[0][2].copy()
    for index, (key_frame, upper, lower) in enumerate(keys[:-1]):
        next_frame, next_upper, next_lower = keys[index + 1]
        if frame <= next_frame:
            t = smooth((frame - key_frame) / (next_frame - key_frame))
            return nlerp(upper, next_upper, t), nlerp(lower, next_lower, t)
    return keys[-1][1].copy(), keys[-1][2].copy()


carry = sample_pose(carry_path)
idle_pose = sample_pose(idle_path)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(idle_path), use_anim=True, ignore_leaf_bones=False)
scene = bpy.context.scene
rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
clip = bpy.data.actions.new("Throw")
clip.use_fake_user = True
rig.animation_data_create().action = clip
if clip.slots:
    rig.animation_data.action_slot = clip.slots[0]

idle = idle_pose["basis"]

for frame in range(FIRST, LAST + 1):
    for bone in ordered:
        bone.matrix_basis = idle[bone.name]
    bpy.context.view_layer.update()

    action = 1.0 - (smooth((frame - RELEASE) / (LAST - RELEASE)) if frame > RELEASE else 0.0)
    if 8 <= frame <= 28:
        dip = -0.018 * math.sin(math.pi * (frame - 8) / 20.0)
        rig.pose.bones["Hips"].location.y += dip
        spine = rig.pose.bones["Spine"]
        spine.rotation_mode = "XYZ"
        spine.rotation_euler.x += (-0.08 if frame < LIFT else 0.10) * math.sin(math.pi * (frame - 8) / 20.0)
    bpy.context.view_layer.update()

    s_loc, s_rot, s_scale = idle["Shoulder.R"].decompose()
    c_loc, c_rot, c_scale = carry["basis"]["Shoulder.R"].decompose()
    rig.pose.bones["Shoulder.R"].matrix_basis = Matrix.LocRotScale(
        s_loc.lerp(c_loc, action),
        s_rot.slerp(c_rot, action),
        s_scale,
    )
    bpy.context.view_layer.update()

    upper_dir, lower_dir = dirs_at(frame, carry, idle_pose)
    aim(rig.pose.bones["UpperArm.R"], upper_dir)
    aim(rig.pose.bones["Arm.R"], lower_dir)

    grip = 1.0 if frame <= RELEASE else 1.0 - smooth((frame - RELEASE) / 8.0)
    for name in FINGERS_R:
        loc, rot, scale = idle[name].decompose()
        c_loc, c_rot, c_scale = carry["basis"][name].decompose()
        rig.pose.bones[name].matrix_basis = Matrix.LocRotScale(
            loc.lerp(c_loc, grip),
            rot.slerp(c_rot, grip),
            scale,
        )

    hand = rig.pose.bones["Hand.R"]
    align = smooth((frame - SIDE) / (RELEASE - SIDE)) if frame > SIDE else 0.0
    aim(hand, lower_dir)
    throw_q = hand.matrix.to_quaternion().copy()
    held_q = carry["hand_q"].slerp(throw_q, align)
    if frame <= RELEASE:
        hand_q = held_q
    else:
        hand_q = held_q.slerp(idle_pose["hand_q"], smooth((frame - RELEASE) / (LAST - RELEASE)))
    hand.matrix = Matrix.LocRotScale(hand.head, hand_q, Vector((1.0, 1.0, 1.0)))
    bpy.context.view_layer.update()
    key_pose(rig, frame)

for label, frame in (("START", FIRST), ("SIDE", SIDE), ("LIFT", LIFT), ("THROW", RELEASE), ("END", LAST)):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    upper = rig.pose.bones["UpperArm.R"]
    lower = rig.pose.bones["Arm.R"]
    hand = rig.pose.bones["Hand.R"]
    print(
        label,
        frame,
        "upper",
        tuple(round(v, 3) for v in (upper.tail - upper.head).normalized()),
        "lower",
        tuple(round(v, 3) for v in (lower.tail - lower.head).normalized()),
        "hand",
        tuple(round(v, 3) for v in hand.head),
    )

export_fbx(rig, out_dir / "FirstPlayerCapsule_Throw.fbx", FIRST, LAST)
print("THROW_CLIP", out_dir / "FirstPlayerCapsule_Throw.fbx")
