import bpy

armature = bpy.data.objects.get("DGN_Armature")
if armature is None:
    raise RuntimeError("DGN_Armature missing")

bones = ["Hips", "Spine", "Neck", "Head", "Shoulder.L", "Shoulder.R", "Hand.L", "Hand.R"]
frames = [1, 15, 30, 45, 60]

baseline = {}
for frame in frames:
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    print("FRAME", frame)
    for name in bones:
        bone = armature.pose.bones.get(name)
        if bone is None:
            continue
        matrix = armature.matrix_world @ bone.matrix
        loc = matrix.to_translation()
        quat = matrix.to_quaternion()
        values = (loc.x, loc.y, loc.z, quat.x, quat.y, quat.z, quat.w)
        if frame == frames[0]:
            baseline[name] = values
            delta = 0.0
        else:
            delta = max(abs(a - b) for a, b in zip(values, baseline[name]))
        print(name, "delta=", round(delta, 6), "loc=", tuple(round(v, 4) for v in loc))
