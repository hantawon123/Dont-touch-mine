import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

OUT = Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions\crawl-volume-fixed')

def import_file(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT/name), use_anim=True)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
    body = bpy.data.objects['Body']
    return bpy.context.scene, rig, body

scene, rig, body = import_file('FirstPlayerCapsule_Crawl_Forward.fbx')
key = body.data.shape_keys.key_blocks['Crawl_Waist_Round']
assert key.slider_max >= 1
scene.frame_set(18)
assert key.value > .99, key.value
evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
mesh = evaluated.to_mesh()
assert all(math.isfinite(coordinate) for vertex in mesh.vertices for coordinate in vertex.co)
assert len(mesh.vertices) == 9212
evaluated.to_mesh_clear()

for obj in scene.objects:
    obj.hide_render = obj.type != 'MESH'
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 600
scene.render.resolution_y = 600
scene.render.resolution_percentage = 100
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'SINGLE'
scene.display.shading.single_color = (.58, .66, .72)
scene.display.shading.show_cavity = True
camera = bpy.data.objects.new('Crawl Volume QA', bpy.data.cameras.new('Crawl Volume QA'))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.55
camera.location = (3.8, -5.0, 1.75)
camera.rotation_euler = (Vector((0,0,.78))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath = str(OUT/'Crawl_Volume_After.png')
bpy.ops.render.render(write_still=True)
(OUT/'verification.json').write_text(json.dumps({'vertices': len(body.data.vertices), 'frame': 18, 'crawl_waist_value': key.value, 'finite_mesh': True}, indent=2))
print('VERIFIED', key.value, len(body.data.vertices))
