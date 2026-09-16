import bpy
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/arm-reference'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=sys.argv[-1], use_scripts=False)
scene = bpy.context.scene
for obj in scene.objects:
    if obj.type == 'MESH':
        print('MESH', obj.name, len(obj.data.vertices), [(m.name, m.type) for m in obj.modifiers], flush=True)
    elif obj.type == 'ARMATURE':
        print('RIG', obj.name, [(b.name, tuple(b.head_local), tuple(b.tail_local)) for b in obj.data.bones if 'Arm' in b.name or 'Hand' in b.name], flush=True)
        obj.data.pose_position = 'REST'
for obj in scene.objects:
    obj.hide_render = obj.type != 'MESH'
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.display.shading.color_type = 'SINGLE'
scene.display.shading.single_color = (.65, .67, .7)
cam = bpy.data.objects.new('QA', bpy.data.cameras.new('QA'))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 2.7
for name, location in [('front', (0, -6, 1)), ('side', (6, 0, 1))]:
    cam.location = location
    cam.rotation_euler = (Vector((0, 0, 1)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(OUT / (name + '.png'))
    bpy.ops.render.render(write_still=True)
