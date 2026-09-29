import bpy

for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    print("MESH", obj.name, "verts", len(obj.data.vertices))
    print("GROUPS", [group.name for group in obj.vertex_groups])
