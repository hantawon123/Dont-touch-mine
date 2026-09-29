import bpy

armature = bpy.data.objects.get("DGN_Armature")
if armature is None:
    raise RuntimeError("DGN_Armature missing")

tracked = [
    "Hips",
    "UpperLeg.L",
    "Leg.L",
    "Foot.L",
    "FootToe1.L",
    "UpperLeg.R",
    "Leg.R",
    "Foot.R",
    "FootToe1.R",
    "Spine",
    "Head",
    "Hand.L",
    "Hand.R",
]

baseline = {}
for frame in [1, 15, 30, 45, 60]:
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    print("FRAME", frame)
    for name in tracked:
        bone = armature.pose.bones.get(name)
        if bone is None:
            continue
        matrix = armature.matrix_world @ bone.matrix
        loc = matrix.to_translation()
        quat = matrix.to_quaternion()
        values = (loc.x, loc.y, loc.z, quat.x, quat.y, quat.z, quat.w)
        if frame == 1:
            baseline[name] = values
            delta = 0.0
        else:
            delta = max(abs(a - b) for a, b in zip(values, baseline[name]))
        print(name, "delta=", round(delta, 6), "loc=", tuple(round(v, 4) for v in loc))
