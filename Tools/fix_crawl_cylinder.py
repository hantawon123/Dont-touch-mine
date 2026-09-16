"""Solve a torso corrective against skinning; preserve the resting cross section."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[1]
OUT=Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
DEST=OUT/'crawl-cylinder';DEST.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];body=bpy.data.objects['Body'];meshes=[o for o in scene.objects if o.type=='MESH']
manifest=json.loads((OUT/'manifest.json').read_text());affected=[a for a in manifest if a['clip']=='Idle' or 'Crawl' in a['clip'] or 'Prone' in a['clip']]
keys=body.data.shape_keys;keys.animation_data_clear()
for k in keys.key_blocks:k.value=0
base=[p.co.copy() for p in keys.key_blocks[0].data];waist=keys.key_blocks['Crawl_Waist_Round']
transform=rig.matrix_world.inverted()@body.matrix_world;inv=transform.inverted()
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
weights=[];masks=[]
for v in body.data.vertices:
 ws=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups if body.vertex_groups[g.group].name in rig.data.bones and rig.data.bones[body.vertex_groups[g.group].name].use_deform]
 total=sum(w for n,w in ws);weights.append([(n,w/total) for n,w in ws] if total else [])
 x,y,z=v.co
 mask=smooth(.48,.67,y)*(1-smooth(1.02,1.28,y))*(1-smooth(.29,.43,abs(x)))
 masks.append(mask)
rig.animation_data.action=bpy.data.actions['Crawl_Forward_CurrentBear']
delta=[Vector() for p in base]
for frame in [0,9,18,27]:
 scene.frame_set(frame);bpy.context.view_layer.update()
 skin={p.name:inv@p.matrix@p.bone.matrix_local.inverted()@transform for p in rig.pose.bones}
 torso=skin['Spine']
 for i,(p,ws,mask) in enumerate(zip(base,weights,masks)):
  if mask<1e-6 or not ws:continue
  blend=Matrix([[sum(skin[n][j][k]*w for n,w in ws) for k in range(4)] for j in range(4)])
  current=blend@p;wanted=torso@p
  correction=blend.to_3x3().inverted_safe()@((wanted-current)*mask)
  delta[i]+=correction/4
for p,orig,d in zip(waist.data,base,delta):p.co=orig+d
for name in ['Crawl_Follow_L','Crawl_Follow_R']:
 # Clear obsolete corrective geometry; its old curves otherwise conflict with the cylinder.
 for p,orig in zip(keys.key_blocks[name].data,base):p.co=orig
for a in affected:
 action=bpy.data.actions[a['clip']+'_CurrentBear_Shapes'];keys.animation_data_create().action=action
 rig.animation_data.action=bpy.data.actions[a['clip']+'_CurrentBear']
 for f in range(a['end']+1):
  scene.frame_set(f);bpy.context.view_layer.update()
  # Ramp with pelvis inclination during entering/leaving prone.
  axis=(rig.pose.bones['Hips'].tail-rig.pose.bones['Hips'].head).normalized()
  amount=smooth(.3,.85,abs(axis.z)) if a['clip']!='Idle' else 0
  waist.value=amount;waist.keyframe_insert('value',frame=f)
  for n in ['Crawl_Follow_L','Crawl_Follow_R']:
   keys.key_blocks[n].value=0;keys.key_blocks[n].keyframe_insert('value',frame=f)
 for layer in action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for curve in bag.fcurves:
     for point in curve.keyframe_points:point.interpolation='LINEAR'
rig.animation_data.action=bpy.data.actions['Crawl_Forward_CurrentBear'];keys.animation_data.action=bpy.data.actions['Crawl_Forward_CurrentBear_Shapes'];scene.frame_start=0;scene.frame_end=35;scene.frame_set(18)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_CrawlCylinder.blend'))
(DEST/'corrective-report.json').write_text(json.dumps({'changed_vertices':sum(d.length>1e-6 for d in delta),'max_delta':max(d.length for d in delta),'rig_actions_edited':False,'clips':[a['clip'] for a in affected]},indent=2))
code=(ROOT/'Tools/fix_crawl_body_volume.py').read_text();exec(code[code.index('# Bake the render surface once'):])
