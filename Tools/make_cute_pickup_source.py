"""Turn the Mixamo pickup reference into an arm-only pickup and carry pose.

Blender --background --python Tools/make_cute_pickup_source.py -- INPUT.fbx OUTPUT.blend
"""
import sys

import bpy


input_path, output_path = sys.argv[sys.argv.index("--") + 1 :]
bpy.ops.import_scene.fbx(filepath=input_path)
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
source = rig.animation_data.action
first, last = (int(value) for value in source.frame_range)
scene = bpy.context.scene

ARM_BONES = {
    "Shoulder.L", "UpperArm.L", "Arm.L", "Hand.L", "Finger_M1.L", "Finger_M2.L", "Finger_T1.L", "Finger_T2.L",
    "Shoulder.R", "UpperArm.R", "Arm.R", "Hand.R", "Finger_M1.R", "Finger_M2.R", "Finger_T1.R", "Finger_T2.R",
}
HOLD_SOURCE_FRAME = 52
PICKUP_LAST_FRAME = 60

rig.animation_data.action = source
baseline = {}
for frame in range(first, last + 1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    if frame == first:
        baseline = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

quiet = bpy.data.actions.new("Pickup_Low")
quiet.use_fake_user = True
rig.animation_data.action = source
for frame in range(1, PICKUP_LAST_FRAME + 2):
    source_frame = round(first + (HOLD_SOURCE_FRAME - first) * (frame - 1) / PICKUP_LAST_FRAME)
    scene.frame_set(source_frame)
    bpy.context.view_layer.update()
    pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
    rig.animation_data.action = quiet
    for bone in rig.pose.bones:
        matrix = pose[bone.name] if bone.name in ARM_BONES else baseline[bone.name]
        bone.matrix_basis = matrix
        bone.rotation_mode = "QUATERNION"
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)
    rig.animation_data.action = source

carry = bpy.data.actions.new("Carry_Idle")
carry.use_fake_user = True
rig.animation_data.action = quiet
scene.frame_set(PICKUP_LAST_FRAME + 1)
bpy.context.view_layer.update()
hold_pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
rig.animation_data.action = carry
for frame in range(1, 62):
    for bone in rig.pose.bones:
        bone.matrix_basis = hold_pose[bone.name]
        bone.rotation_mode = "QUATERNION"
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)

for action in (quiet, carry):
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"

rig.animation_data.action = quiet
scene.frame_start = 1
scene.frame_end = PICKUP_LAST_FRAME
scene.render.fps = 30
bpy.ops.wm.save_as_mainfile(filepath=output_path)
print(f"CREATED arm-only Pickup_Low 1..{PICKUP_LAST_FRAME}; Carry_Idle 1..61; {output_path}")
