import bpy,json,math,numpy as np
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'source/blender/characters/ReferenceBear_V13'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'),use_scripts=False)
s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];body=bpy.data.objects['Body'];ctrl=bpy.data.objects['CUSTOMIZE']
parts=[o for o in s.objects if o.type=='MESH' and o.get('customization_slot')]
report={'parts':{},'clips':{},'controls':{}}
for o in parts:
    assert any(m.type=='ARMATURE' and m.object==r for m in o.modifiers),o.name
    assert all(v.groups for v in o.data.vertices),o.name
    report['parts'][o.name]={'vertices':len(o.data.vertices),'slot':o['customization_slot'],'weighted':True}
def activate(name):
    a=bpy.data.actions['First_'+name];r.animation_data.action=a
    if a.slots:r.animation_data.action_slot=a.slots[0]
    keys=body.data.shape_keys;keys.animation_data_clear()
    for k in keys.key_blocks:k.value=0
    a=bpy.data.actions.get('Shapes_'+name)
    if a:
        keys.animation_data_create().action=a
        if a.slots:keys.animation_data.action_slot=a.slots[0]
    s.frame_set(1);bpy.context.view_layer.update()
for a in list(bpy.data.actions):
    if not a.name.startswith('First_'):continue
    name=a.name[6:];activate(name);frames=[int(a.frame_range[0]),int(sum(a.frame_range)/2),int(a.frame_range[1])]
    for f in frames:
        s.frame_set(f);dep=bpy.context.evaluated_depsgraph_get()
        for o in parts:
            ev=o.evaluated_get(dep);me=ev.to_mesh()
            coords=np.empty(len(me.vertices)*3,dtype=np.float32);me.vertices.foreach_get('co',coords);assert np.isfinite(coords).all() and np.max(np.abs(coords))<10,(name,o.name,f)
            ev.to_mesh_clear()
    report['clips'][name]=frames
activate('Idle')
for pn in list(ctrl.keys()):
    ctrl[pn]=1.;ctrl.update_tag();s.frame_set(2);bpy.context.view_layer.update()
    keys=[k for o in parts if o.data.shape_keys for k in o.data.shape_keys.key_blocks if k.name==pn]
    assert keys and all(abs(k.value-1)<1e-5 for k in keys),pn
    report['controls'][pn]='driver evaluated at 1'
    ctrl[pn]=0.;ctrl.update_tag();s.frame_set(1)
(OUT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('VERIFIED',len(report['clips']),len(report['controls']),flush=True)
s.render.engine='CYCLES';s.cycles.samples=12;s.render.resolution_percentage=65
for name,cam in [('Idle','ThreeQuarter'),('Crouch_Idle','ThreeQuarter'),('Crawl_Forward','ThreeQuarter'),('Carry_TwoHands','ThreeQuarter')]:
    activate(name);s.camera=bpy.data.objects[cam]
    dep=bpy.context.evaluated_depsgraph_get();points=[o.matrix_world@Vector(c) for o in parts for c in o.evaluated_get(dep).bound_box]
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(low+high)/2
    s.camera.location=center+Vector((3,-7,2.4));s.camera.rotation_euler=(center-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=max(2.23,(high-low).length*1.05)
    s.render.filepath=str(OUT/('preview-'+name+'.png'));bpy.ops.render.render(write_still=True)
print('DONE',flush=True)

