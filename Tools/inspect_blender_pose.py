import math
import bpy

for armature in [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]:
    print("ARMATURE", armature.name)
    for bone in armature.pose.bones:
        loc = bone.location
        rot = bone.rotation_euler if bone.rotation_mode != "QUATERNION" else bone.rotation_quaternion.to_euler()
        scale = bone.scale
        changed = (
            abs(loc.x) > 1e-5 or abs(loc.y) > 1e-5 or abs(loc.z) > 1e-5 or
            abs(rot.x) > 1e-5 or abs(rot.y) > 1e-5 or abs(rot.z) > 1e-5 or
            abs(scale.x - 1) > 1e-5 or abs(scale.y - 1) > 1e-5 or abs(scale.z - 1) > 1e-5
        )
        if changed:
            print(
                bone.name,
                "loc=", tuple(round(v, 4) for v in loc),
                "rot_deg=", tuple(round(math.degrees(v), 2) for v in rot),
                "scale=", tuple(round(v, 4) for v in scale),
            )
