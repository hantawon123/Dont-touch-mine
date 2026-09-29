import math
import sys
from mathutils import Vector

import bpy

output_path = sys.argv[-1]

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not meshes:
    raise RuntimeError("No mesh objects found")

min_corner = Vector((float("inf"), float("inf"), float("inf")))
max_corner = Vector((float("-inf"), float("-inf"), float("-inf")))
for obj in meshes:
    for corner in obj.bound_box:
        world = obj.matrix_world @ Vector(corner)
        min_corner.x = min(min_corner.x, world.x)
        min_corner.y = min(min_corner.y, world.y)
        min_corner.z = min(min_corner.z, world.z)
        max_corner.x = max(max_corner.x, world.x)
        max_corner.y = max(max_corner.y, world.y)
        max_corner.z = max(max_corner.z, world.z)

center = (min_corner + max_corner) * 0.5
size = max((max_corner - min_corner).length, 1.0)

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = center + Vector((0.0, -size * 2.4, size * 0.45))
direction = center - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
camera_data.lens = 70
bpy.context.scene.camera = camera

light_data = bpy.data.lights.new("PreviewKey", "AREA")
light = bpy.data.objects.new("PreviewKey", light_data)
bpy.context.collection.objects.link(light)
light.location = center + Vector((size * 0.8, -size * 1.1, size * 1.8))
light_data.energy = 400
light_data.size = size

bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
bpy.context.scene.display.shading.light = "STUDIO"
bpy.context.scene.display.shading.color_type = "MATERIAL"
bpy.context.scene.render.resolution_x = 1200
bpy.context.scene.render.resolution_y = 1200
bpy.context.scene.render.film_transparent = False
bpy.context.scene.view_settings.view_transform = "Standard"

bpy.ops.render.render(write_still=False)
bpy.data.images["Render Result"].save_render(filepath=output_path)
print("RENDERED", output_path)
