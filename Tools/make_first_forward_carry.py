"""Build a natural forward carry pose from First's idle and the prior carry reference.

Blender --background --python Tools/make_first_forward_carry.py -- IDLE.blend OLD_CARRY.blend CARRY.blend PICKUP.blend
"""
import sys
from math import pi, sin
from pathlib import Path

import bpy
from mathutils import Matrix


idle_path, old_carry_path, carry_output, pickup_output = sys.argv[sys.argv.index("--") + 1 :]
idle_action_name = "Idle_Breathing_2s_First_Baked"
last_frame = 60
weights = {
    "Shoulder.R": 0.55,
    "UpperArm.R": 0.75,
    "Arm.R": 0.75,
    "Hand.R": 0.70,
    "Finger_M1.R": 0.55,
    "Finger_M2.R": 0.55,
    "Finger_T1.R": 0.55,
    "Finger_T2.R": 0.55,
    "Shoulder.L": 0.10,
    "UpperArm.L": 0.20,
    "Arm.L": 0.20,
    "Hand.L": 0.20,
    "Finger_M1.L": 0.25,
    "Finger_M2.L": 0.25,
    "Finger_T1.L": 0.25,
    "Finger_T2.L": 0.25,
}


def activate(rig, action):
    rig.animation_data_create().action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]


bpy.ops.wm.open_mainfile(filepath=idle_path, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects["DGN_Armature"]
activate(rig, bpy.data.actions[idle_action_name])
idle_poses = {}
for frame in range(1, last_frame + 2):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    idle_poses[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

bpy.ops.wm.open_mainfile(filepath=old_carry_path, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects["DGN_Armature"]
old_carry = bpy.data.actions.get("Carry_Idle_First_Baked") or bpy.data.actions["Carry_Idle"]
activate(rig, old_carry)
scene.frame_set(31)
bpy.context.view_layer.update()
held_pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

bpy.ops.wm.open_mainfile(filepath=idle_path, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects["DGN_Armature"]


def write_action(name, pickup):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    activate(rig, action)
    for frame in range(1, last_frame + 2):
        progress = min(1.0, (frame - 1) / 40.0) if pickup else 1.0
        for bone in rig.pose.bones:
            base = idle_poses[frame][bone.name]
            weight = weights.get(bone.name, 0.0) * progress
            if weight:
                base_loc, base_rot, base_scale = base.decompose()
                held_loc, held_rot, held_scale = held_pose[bone.name].decompose()
                bone.matrix_basis = Matrix.LocRotScale(
                    base_loc.lerp(held_loc, weight),
                    base_rot.slerp(held_rot, weight),
                    base_scale.lerp(held_scale, weight),
                )
            else:
                bone.matrix_basis = base
            if not pickup and bone.name in {"UpperArm.R", "Arm.R", "Hand.R"}:
                pulse = sin((frame - 1) * 2 * pi / last_frame) * 0.018
                bone.matrix_basis = bone.matrix_basis @ Matrix.Rotation(pulse, 4, "Y")
            bone.rotation_mode = "QUATERNION"
            for property_name in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(property_name, frame=frame, group=bone.name)
    return action


carry = write_action("Carry_Idle", pickup=False)
pickup = write_action("Pickup_Low", pickup=True)
scene.frame_start = 1
scene.frame_end = last_frame
scene.render.fps = 30

activate(rig, carry)
Path(carry_output).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=carry_output)
activate(rig, pickup)
Path(pickup_output).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=pickup_output)
print(f"CREATED forward Carry_Idle and arm-only Pickup_Low: {carry_output}, {pickup_output}")
