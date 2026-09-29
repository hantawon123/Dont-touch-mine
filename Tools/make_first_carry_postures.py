"""Overlay a right-hand carry onto crouch and prone clips.

Crouch keeps the source squat and lifts the right arm above the default
crouch hang. Prone keeps the left hand planted, lifts the right arm a
little off the floor, and stands the hand on its side.

Blender --background --python Tools/make_first_carry_postures.py -- CARRY.fbx OUT_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


carry_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)

CROUCH = (
    ("FirstPlayerCapsule_Crouch_Idle.fbx", "Carry_Crouch_Idle"),
    ("FirstPlayerCapsule_Crouch_Walk_Forward.fbx", "Carry_Crouch_Walk_Forward"),
    ("FirstPlayerCapsule_Crouch_Walk_Back.fbx", "Carry_Crouch_Walk_Back"),
    ("FirstPlayerCapsule_Crouch_Walk_Left.fbx", "Carry_Crouch_Walk_Left"),
    ("FirstPlayerCapsule_Crouch_Walk_Right.fbx", "Carry_Crouch_Walk_Right"),
)
PRONE = (
    ("FirstPlayerCapsule_Prone_Idle.fbx", "Carry_Prone_Idle"),
    ("FirstPlayerCapsule_Crawl_Forward.fbx", "Carry_Crawl_Forward"),
    ("FirstPlayerCapsule_Crawl_Back.fbx", "Carry_Crawl_Back"),
    ("FirstPlayerCapsule_Crawl_Left.fbx", "Carry_Crawl_Left"),
    ("FirstPlayerCapsule_Crawl_Right.fbx", "Carry_Crawl_Right"),
)
FINGERS = ("Finger_M1.R", "Finger_M2.R", "Finger_T1.R", "Finger_T2.R")
ARM_LIFT = ("Shoulder.R", "UpperArm.R", "Arm.R")


def descendants(bone):
    names = [bone.name]
    for child in bone.children:
        names.extend(descendants(child))
    return names


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


def raise_toward_up(bone, degrees):
    direction = bone.tail - bone.head
    if direction.length < 1e-8 or abs(degrees) < 1e-6:
        return
    direction.normalize()
    axis = direction.cross(Vector((0.0, 1.0, 0.0)))
    if axis.length < 1e-6:
        axis = Vector((1.0, 0.0, 0.0))
    axis.normalize()
    aim(bone, Matrix.Rotation(math.radians(degrees), 3, axis) @ direction)


def lift_right_arm(rig, degrees):
    scales = {"Shoulder.R": 0.35, "UpperArm.R": 1.0, "Arm.R": 0.55}
    for name in ARM_LIFT:
        raise_toward_up(rig.pose.bones[name], degrees * scales[name])


def stand_hand_sideways(hand):
    length = hand.tail - hand.head
    axis = length.normalized() if abs(length.z) > 0.35 and length.length > 1e-8 else Vector((0.0, 0.0, 1.0))
    rotation = Matrix.Rotation(math.radians(90.0), 4, axis).to_quaternion()
    hand.matrix = Matrix.LocRotScale(hand.head, rotation @ hand.matrix.to_quaternion(), Vector((1.0, 1.0, 1.0)))
    bpy.context.view_layer.update()


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def key_shapes(body, frame, values):
    if body is None or body.data.shape_keys is None:
        return
    keys = body.data.shape_keys
    keys.animation_data_create()
    for name, value in values.items():
        block = keys.key_blocks.get(name)
        if block is None:
            continue
        block.value = value
        block.keyframe_insert("value", frame=frame)


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


def bake(source_name, clip_name, held, mode):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(out_dir / source_name), use_anim=True, ignore_leaf_bones=False)
    scene = bpy.context.scene
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    body = next((obj for obj in scene.objects if obj.type == "MESH" and obj.data.shape_keys), None)
    action = rig.animation_data.action
    first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    samples = {}
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        shapes = {}
        if body is not None:
            shapes = {key.name: key.value for key in body.data.shape_keys.key_blocks[1:]}
        samples[frame] = (
            {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones},
            shapes,
        )

    clip = bpy.data.actions.new(clip_name)
    clip.use_fake_user = True
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    if body is not None and body.data.shape_keys is not None:
        body.data.shape_keys.animation_data_create().action = bpy.data.actions.new(f"{clip_name}_shapes")

    for frame in range(first, last + 1):
        basis, shapes = samples[frame]
        for bone in ordered:
            bone.matrix_basis = basis[bone.name]
        bpy.context.view_layer.update()
        if mode == "crouch":
            lift_right_arm(rig, 22.0)
        else:
            lift_right_arm(rig, 14.0)
            stand_hand_sideways(rig.pose.bones["Hand.R"])
        for name in FINGERS:
            if name in rig.pose.bones and name in held:
                rig.pose.bones[name].matrix_basis = held[name]
        bpy.context.view_layer.update()
        key_pose(rig, frame)
        key_shapes(body, frame, shapes)

    hand = rig.pose.bones["Hand.R"]
    axis = hand.tail - hand.head
    print(
        f"{clip_name} Hand.R=({hand.head.x:+.3f},{hand.head.y:+.3f},{hand.head.z:+.3f}) "
        f"axis=({axis.x:+.3f},{axis.y:+.3f},{axis.z:+.3f})"
    )
    export_fbx(rig, out_dir / f"FirstPlayerCapsule_{clip_name}.fbx", first, last)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=carry_path, use_anim=True, ignore_leaf_bones=False)
carry_rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
carry_bones = [name for name in descendants(carry_rig.pose.bones["Shoulder.R"]) if name in FINGERS]
frame = int(carry_rig.animation_data.action.frame_range[0])
bpy.context.scene.frame_set(frame)
bpy.context.view_layer.update()
held = {name: carry_rig.pose.bones[name].matrix_basis.copy() for name in carry_bones}
print("CARRY_BONES", carry_bones)

for source_name, clip_name in CROUCH:
    bake(source_name, clip_name, held, "crouch")
for source_name, clip_name in PRONE:
    bake(source_name, clip_name, held, "prone")

print("CARRY_POSTURE_CLIPS", out_dir)
