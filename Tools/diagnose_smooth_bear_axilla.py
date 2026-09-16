import bpy, sys, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/axilla'
OUT.mkdir(exist_ok=True)
SOURCE=Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/supplied-bear-smooth/BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
src=bpy.data.objects['Body']
weights=[{src.vertex_groups[g.group].name:g.weight for g in v.groups} for v in src.data.vertices]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'),use_scripts=False)
body=bpy.data.objects['Body'];rig=bpy.data.objects['DGN_Armature']
baseweights=[{body.vertex_groups[g.group].name:g.weight for g in v.groups} for v in body.data.vertices]
existing=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Temp/smooth-bear-unity-qa/Assets/Source/FirstPlayerCapsule_Idle.fbx'))
added=set(bpy.data.objects)-existing
motion=next(o for o in added if o.type=='ARMATURE')
bpy.context.scene.frame_set(10)
bpy.context.view_layer.update()
rig.animation_data_clear();rig.data.pose_position='POSE'
for b in rig.pose.bones: b.matrix_basis=motion.pose.bones[b.name].matrix_basis.copy()
for o in added:bpy.data.objects.remove(o,do_unlink=True)
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=720;scene.render.resolution_y=820;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='SINGLE';scene.display.shading.single_color=(.8,.74,.42)
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
cam=bpy.data.objects.new('AxillaCamera',bpy.data.cameras.new('AxillaCamera'));scene.collection.objects.link(cam);scene.camera=cam
cam.data.type='ORTHO';cam.data.ortho_scale=2.35
cam.location=(0,-5,1.05);cam.rotation_euler=(Vector((0,0,1.05))-cam.location).to_track_quat('-Z','Y').to_euler()
def render(name):
    bpy.context.view_layer.update();scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
def restore_upper():
    for v,w in zip(body.data.vertices,weights):
        t=max(0,min(1,(v.co.y-.65)/.20));t=t*t*(3-2*t)
        if not t:continue
        old=baseweights[v.index]
        for g in body.vertex_groups:g.remove([v.index])
        for n in old.keys()|w.keys():body.vertex_groups[n].add([v.index],old.get(n,0)*(1-t)+w.get(n,0)*t,'REPLACE')
render('current-idle')
restore_upper();render('source-weights-idle')
smooth=body.modifiers.new('Source joint smoothing','CORRECTIVE_SMOOTH');smooth.factor=.6;smooth.iterations=16;smooth.smooth_type='LENGTH_WEIGHTED';smooth.rest_source='ORCO'
render('source-smoothing-idle')
print('AXILLA_DIAG_DONE',flush=True)
