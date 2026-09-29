import bpy
import sys

fbx_path = sys.argv[-1]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

print("IMPORTED", fbx_path)
print("OBJECTS")
for obj in bpy.data.objects:
    print(obj.name, obj.type, "parent=", obj.parent.name if obj.parent else "")

print("ARMATURES")
for obj in bpy.data.objects:
    if obj.type == "ARMATURE":
        print("ARMATURE", obj.name, "bones=", len(obj.data.bones))

print("ACTIONS")
for action in bpy.data.actions:
    print(action.name, tuple(action.frame_range), len(action.fcurves))
