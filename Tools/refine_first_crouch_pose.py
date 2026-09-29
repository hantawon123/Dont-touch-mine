"""Widen First's crouch stance and keep its arms slightly forward.

Blender --background --python Tools/refine_first_crouch_pose.py -- INPUT.blend ACTION LAST_FRAME OUTPUT.blend
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


input_path, action_name, last_frame, output_path = sys.argv[sys.argv.index("--") + 1 :]
last_frame = int(last_frame)
bpy.ops.wm.open_mainfile(filepath=input_path, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects["DGN_Armature"]
source = bpy.data.actions[action_name]
rig.animation_data_create().action = source
if source.slots:
    rig.animation_data.action_slot = source.slots[0]

samples = {}
for frame in range(1, last_frame + 2):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    samples[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

refined = bpy.data.actions.new(f"{action_name}_Refined")
refined.use_fake_user = True
rig.animation_data.action = refined
if refined.slots:
    rig.animation_data.action_slot = refined.slots[0]


def rotation_sign(bone, local_axis, world_axis, desired_component):
    rotation_axis = bone.matrix.to_3x3() @ local_axis
    bone_direction = bone.matrix.to_3x3() @ Vector((0, 1, 0))
    derivative = rotation_axis.cross(bone_direction)
    return 1.0 if derivative[world_axis] * desired_component >= 0 else -1.0


for frame, pose in samples.items():
    scene.frame_set(frame)
    for bone in rig.pose.bones:
        bone.matrix_basis = pose[bone.name]

    # Push each knee outward about 10 degrees. This gives the mesh room at the
    # crotch without moving the pelvis or changing the crouch height.
    for name, desired_x in (("UpperLeg.L", 1.0), ("UpperLeg.R", -1.0)):
        bone = rig.pose.bones[name]
        sign = rotation_sign(bone, Vector((0, 0, 1)), 0, desired_x)
        bone.matrix_basis = bone.matrix_basis @ Matrix.Rotation(sign * 0.18, 4, "Z")
        bpy.context.view_layer.update()

    # Hang the upper arms and fold the forearms forward so the silhouette is ㄴ,
    # not ㄱ. World +Z is character forward after the Unity FBX round-trip.
    desired = {
        "UpperArm.L": Vector((0.20, -0.94, 0.28)),
        "UpperArm.R": Vector((-0.20, -0.94, 0.28)),
        "Arm.L": Vector((0.08, -0.12, 0.99)),
        "Arm.R": Vector((-0.08, -0.12, 0.99)),
    }
    for name, direction in desired.items():
        bone = rig.pose.bones[name]
        current = bone.tail - bone.head
        if current.length < 1e-8:
            continue
        rotation = current.normalized().rotation_difference(direction.normalized())
        axes = bone.matrix.to_3x3()
        local = axes.inverted() @ rotation.to_matrix() @ axes
        bone.matrix_basis = bone.matrix_basis @ local.to_4x4()
        bpy.context.view_layer.update()

    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)

scene.frame_start = 1
scene.frame_end = last_frame
scene.render.fps = 30
Path(output_path).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=output_path)
print(f"REFINED {action_name}: outward knees and forward ㄴ arms; {output_path}")
