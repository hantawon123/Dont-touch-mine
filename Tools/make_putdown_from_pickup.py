"""Create a First put-down clip by reversing the approved pickup action.

Blender --background --python Tools/make_putdown_from_pickup.py -- INPUT.blend OUTPUT.blend
"""
import sys
from pathlib import Path

import bpy


input_path, output_path = sys.argv[sys.argv.index("--") + 1 :]
bpy.ops.wm.open_mainfile(filepath=input_path, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects["DGN_Armature"]
pickup = bpy.data.actions["Pickup_Low"]
rig.animation_data_create().action = pickup
if pickup.slots:
    rig.animation_data.action_slot = pickup.slots[0]

last_frame = 60
poses = {}
for frame in range(1, last_frame + 2):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    poses[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

putdown = bpy.data.actions.new("PutDown_Low")
putdown.use_fake_user = True
rig.animation_data.action = putdown
if putdown.slots:
    rig.animation_data.action_slot = putdown.slots[0]
for frame in range(1, last_frame + 2):
    for bone in rig.pose.bones:
        bone.matrix_basis = poses[last_frame + 2 - frame][bone.name]
        bone.rotation_mode = "QUATERNION"
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)

scene.frame_start = 1
scene.frame_end = last_frame
scene.render.fps = 30
Path(output_path).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=output_path)
print(f"CREATED PutDown_Low 1..{last_frame}: {output_path}")
