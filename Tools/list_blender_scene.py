import bpy

print("SCENE", bpy.context.scene.name)
print("FRAME_RANGE", bpy.context.scene.frame_start, bpy.context.scene.frame_end, bpy.context.scene.render.fps)

print("OBJECTS")
for obj in bpy.data.objects:
    print(obj.name, obj.type, "parent=", obj.parent.name if obj.parent else "")

print("ARMATURES")
for obj in bpy.data.objects:
    if obj.type == "ARMATURE":
        print("ARMATURE", obj.name)
        for bone in obj.data.bones:
            print("  BONE", bone.name, "parent=", bone.parent.name if bone.parent else "")

print("ACTIONS")
for action in bpy.data.actions:
    print(action.name, "range=", tuple(action.frame_range), "curves=", len(action.fcurves))
