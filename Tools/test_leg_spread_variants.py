import math

import bpy
from mathutils import Quaternion, Vector

armature = bpy.data.objects.get("DGN_Armature")
if armature is None:
    raise RuntimeError("DGN_Armature missing")

variants = [
    ("x", Vector((1, 0, 0))),
    ("y", Vector((0, 1, 0))),
    ("z", Vector((0, 0, 1))),
]

def foot_gap():
    bpy.context.view_layer.update()
    left = armature.matrix_world @ armature.pose.bones["Foot.L"].matrix.to_translation()
    right = armature.matrix_world @ armature.pose.bones["Foot.R"].matrix.to_translation()
    return abs(left.x - right.x), tuple(round(v, 4) for v in left), tuple(round(v, 4) for v in right)

for axis_name, axis in variants:
    for sign_mode in [1, -1]:
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        for side, sign in [("L", 1), ("R", -1)]:
            bone = armature.pose.bones[f"UpperLeg.{side}"]
            bone.rotation_mode = "QUATERNION"
            angle = math.radians(10) * sign * sign_mode
            bone.rotation_quaternion = Quaternion(axis, angle) @ bone.rotation_quaternion
        gap, left, right = foot_gap()
        print(f"variant axis={axis_name} sign_mode={sign_mode} gap={gap:.4f} L={left} R={right}")
