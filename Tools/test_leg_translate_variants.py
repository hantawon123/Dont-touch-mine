import bpy

armature = bpy.data.objects.get("DGN_Armature")
if armature is None:
    raise RuntimeError("DGN_Armature missing")

chains = [
    ("upper", ["UpperLeg.L", "UpperLeg.R"]),
    ("full", ["UpperLeg.L", "Leg.L", "Foot.L", "FootToe1.L", "UpperLeg.R", "Leg.R", "Foot.R", "FootToe1.R"]),
]

def foot_gap():
    bpy.context.view_layer.update()
    left = armature.matrix_world @ armature.pose.bones["Foot.L"].matrix.to_translation()
    right = armature.matrix_world @ armature.pose.bones["Foot.R"].matrix.to_translation()
    return abs(left.x - right.x), tuple(round(v, 4) for v in left), tuple(round(v, 4) for v in right)

for label, bones in chains:
    for offset in [0.02, 0.04, 0.06, 0.08]:
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        for name in bones:
            bone = armature.pose.bones[name]
            side = 1 if name.endswith(".L") else -1
            bone.location.x += offset * side
        gap, left, right = foot_gap()
        print(f"variant {label} offset={offset:.2f} gap={gap:.4f} L={left} R={right}")
