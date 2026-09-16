import bpy
from pathlib import Path
from mathutils import Vector

OUT = Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'), use_scripts=False)
scene = bpy.context.scene
body = bpy.data.objects['Body']
rig = bpy.data.objects['DGN_Armature']
rig.animation_data.action = bpy.data.actions['Crawl_Forward_CurrentBear']
body.data.shape_keys.animation_data.action = bpy.data.actions['Crawl_Forward_CurrentBear_Shapes']
scene.frame_set(18)
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
cam = bpy.data.objects.new('Crawl QA Camera', bpy.data.cameras.new('Crawl QA Camera'))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 2.55
cam.location = (3.8, -5.0, 1.75)
cam.rotation_euler = (Vector((0,0,.78))-cam.location).to_track_quat('-Z','Y').to_euler()
key = body.data.shape_keys.key_blocks['Crawl_Waist_Round']
key.slider_max = 2.5
for value, name in ((0, 'Crawl_Waist_Before.png'), (1, 'Crawl_Waist_CurrentKey.png'), (2.2, 'Crawl_Waist_Strength22.png')):
    key.value = value
    scene.frame_set(18)
    scene.render.filepath = str(OUT/name)
    bpy.ops.render.render(write_still=True)

# Test a gentle all-around belly volume corrective rather than only pushing the waist depth.
for shape in list(body.data.shape_keys.key_blocks):
    if shape.name == 'Crawl_Belly_Full':
        body.shape_key_remove(shape)
basis = body.data.shape_keys.key_blocks['Basis']
full = body.shape_key_add(name='Crawl_Belly_Full')
for point, base in zip(full.data, basis.data):
    x, y, z = base.co
    band = max(0.0, 1.0 - abs(y - .77) / .58)
    radius = (x*x + z*z) ** .5
    if band and radius > .08:
        point.co = base.co + Vector((x / radius, 0, z / radius)) * (.10 * band * band)
key.value = 0
full.value = 1
scene.frame_set(18)
scene.render.filepath = str(OUT/'Crawl_Belly_Full_Test.png')
bpy.ops.render.render(write_still=True)

# Test a reduced spine bend; legs and arms keep their original walk-cycle keys.
full.value = 0
scene.frame_set(18)
spine = rig.pose.bones['Spine']
original = spine.rotation_quaternion.copy()
spine.rotation_quaternion = original.slerp(type(original)(), .45)
bpy.context.view_layer.update()
scene.render.filepath = str(OUT/'Crawl_Spine_Relaxed_Test.png')
bpy.ops.render.render(write_still=True)
