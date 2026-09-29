"""Add the crouch-only centre-crotch tip correction.

Blender --background --python Tools/add_crouch_groin_correction.py -- INPUT.blend LAST_FRAME START_VALUE END_VALUE OUTPUT.blend
"""
import sys
from pathlib import Path

import bpy


source, last_frame, start_value, end_value, destination = sys.argv[sys.argv.index("--") + 1:]
last_frame = int(last_frame)
start_value = float(start_value)
end_value = float(end_value)
bpy.ops.wm.open_mainfile(filepath=source, use_scripts=False)
body = bpy.data.objects["Body"]

keys = body.data.shape_keys
if not keys:
    body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
key = keys.key_blocks.get("Crouch_Groin_Flat") or body.shape_key_add(name="Crouch_Groin_Flat")
basis = keys.key_blocks[0]
for point, base in zip(key.data, basis.data):
    x, y, z = base.co
    point.co = base.co.copy()
    centre = max(0.0, 1.0 - abs(x) / 0.10) ** 2
    height = max(0.0, 1.0 - abs(y - 0.45) / 0.12) ** 2
    depth = max(0.0, 1.0 - abs(z) / 0.07) ** 2
    weight = centre * height * depth
    if weight > 0:
        point.co.y += 0.10 * weight

key.value = start_value
keys.animation_data_clear()
key.value = start_value
key.keyframe_insert("value", frame=1)
key.value = end_value
key.keyframe_insert("value", frame=last_frame)
action = keys.animation_data.action
action.name = "Crouch_Groin_Flat_Value"
action.use_fake_user = True
for layer in action.layers:
    for strip in layer.strips:
        for slot in action.slots:
            bag = strip.channelbag(slot)
            if bag:
                for curve in bag.fcurves:
                    for point in curve.keyframe_points:
                        point.interpolation = "LINEAR"

scene = bpy.context.scene
scene.frame_start = 1
scene.frame_end = last_frame
scene.frame_set(1)
Path(destination).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=destination)
print("CROUCH_GROIN_CORRECTION", source, destination, start_value, end_value)
