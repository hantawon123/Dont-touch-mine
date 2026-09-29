"""Bake one approved DGN action onto the First character's different rest pose.

Blender --background --python Tools/bake_first_action.py -- SOURCE.blend TARGET.blend ACTION LAST_FRAME OUTPUT.blend
"""
import sys
from pathlib import Path

import bpy


arguments = sys.argv[sys.argv.index("--") + 1:]
source_path, target_path, action_name, last_frame, output_path = arguments[:5]
require_loop_closure = len(arguments) > 5 and arguments[5] == "--loop"
last_frame = int(last_frame)

bpy.ops.wm.open_mainfile(filepath=source_path, use_scripts=False)
source = bpy.data.objects["DGN_Armature"]
action = bpy.data.actions[action_name]
source.animation_data_create().action = action
if action.slots:
    source.animation_data.action_slot = action.slots[0]
source_rest = {bone.name: bone.matrix_local.copy() for bone in source.data.bones}
source_directions = {
    bone.name: (bone.tail_local - bone.head_local).normalized()
    for bone in source.data.bones
}
samples = {}
for frame in range(1, last_frame + 2):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    samples[frame] = {bone.name: bone.matrix.copy() for bone in source.pose.bones}

bpy.ops.wm.open_mainfile(filepath=target_path, use_scripts=False)
scene = bpy.context.scene
target = bpy.data.objects["DGN_Armature"]
target_bone_names = {bone.name for bone in target.data.bones}
if not target_bone_names.issubset(source_rest):
    missing = sorted(target_bone_names.difference(source_rest))
    raise RuntimeError(f"Source rig is missing First bones: {missing}")

# Align source pose matrices to the First rig's rest-axis rolls. This preserves
# the source's visible joint paths while retaining the First mesh and rest pose.
correction = {}
for bone in target.data.bones:
    aligned = (bone.tail_local - bone.head_local).normalized().rotation_difference(source_directions[bone.name])
    correction[bone.name] = (
        source_rest[bone.name].to_quaternion().inverted()
        @ aligned
        @ bone.matrix_local.to_quaternion()
    ).to_matrix().to_4x4()

target.animation_data_clear()
baked = bpy.data.actions.new(f"{action_name}_First_Baked")
baked.use_fake_user = True
target.animation_data_create().action = baked
if baked.slots:
    target.animation_data.action_slot = baked.slots[0]
ordered = sorted(target.pose.bones, key=lambda bone: len(bone.parent_recursive))
for bone in ordered:
    bone.rotation_mode = "QUATERNION"

previous = {}
for frame, source_pose in samples.items():
    scene.frame_set(frame)
    for bone in ordered:
        bone.matrix = source_pose[bone.name] @ correction[bone.name]
        bpy.context.view_layer.update()
        rotation = bone.rotation_quaternion.copy()
        if bone.name in previous and rotation.dot(previous[bone.name]) < 0:
            rotation.negate()
        bone.rotation_quaternion = rotation
        previous[bone.name] = rotation.copy()
        for property_name in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(property_name, frame=frame, group=bone.name)

for layer in baked.layers:
    for strip in layer.strips:
        for slot in baked.slots:
            bag = strip.channelbag(slot)
            if bag:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = "LINEAR"

scene.frame_start = 1
scene.frame_end = last_frame
scene.render.fps = 30
target["motion_notes"] = (
    f"Baked from {action_name}: 30 fps, frames 1-{last_frame}, "
    f"closure {last_frame + 1}; First mesh and rest pose preserved."
)
scene.frame_set(1)
Path(output_path).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=output_path)

# Verify that the cyclic closing pose is exact after saving.
bpy.ops.wm.open_mainfile(filepath=output_path, use_scripts=False)
scene = bpy.context.scene
target = bpy.data.objects["DGN_Armature"]
closure_error = 0.0
scene.frame_set(1)
bpy.context.view_layer.update()
first = {bone.name: bone.matrix.translation.copy() for bone in target.pose.bones}
scene.frame_set(last_frame + 1)
bpy.context.view_layer.update()
for bone in target.pose.bones:
    closure_error = max(closure_error, (bone.matrix.translation - first[bone.name]).length)
if require_loop_closure and closure_error > 0.0001:
    raise RuntimeError(f"Loop closure error: {closure_error}")
print("BAKED", action_name, "end_displacement", closure_error, "OUTPUT", output_path)
