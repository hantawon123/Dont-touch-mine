"""Retime approved clips, add carry jump, and overlap alternating crawl phases."""
import bpy,sys,math
from pathlib import Path
root,out=map(Path,sys.argv[sys.argv.index('--')+1:]);out.mkdir(parents=True,exist_ok=True)
def load(n):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/f'FirstPlayerCapsule_{n}.fbx'));r=bpy.data.objects['DGN_Armature'];return r
def read(r,t):
 bpy.context.scene.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update();k=bpy.data.objects['Body'].data.shape_keys
 return ({b.name:b.matrix_basis.copy() for b in r.pose.bones},{x.name:x.value for x in k.key_blocks[1:]} if k else {})
def export(r,n,frames):
 r=load('Idle_Breathing_2s')
 k=bpy.data.objects['Body'].data.shape_keys;r.animation_data.action=bpy.data.actions.new(n);k.animation_data_clear()
 for f,(pose,shapes) in enumerate(frames):
  for b in r.pose.bones:
   b.matrix_basis=pose[b.name];b.rotation_mode='QUATERNION'
   for p in ('location','rotation_quaternion','scale'):b.keyframe_insert(p,frame=f)
  for x in k.key_blocks[1:]:x.value=shapes.get(x.name,0);x.keyframe_insert('value',frame=f)
 s=bpy.context.scene;s.frame_start=0;s.frame_end=len(frames)-1;s.render.fps=30;s.frame_set(0)
 bpy.ops.export_scene.fbx(filepath=str(out/f'FirstPlayerCapsule_{n}.fbx'),use_selection=False,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(n+'.blend')));print('DONE',n,len(frames)-1)
r=load('Throw');lo,hi=r.animation_data.action.frame_range;export(r,'Throw',[read(r,lo+(hi-lo)*i/24) for i in range(25)])
r=load('Carry_Idle');carry,cs=read(r,r.animation_data.action.frame_range[0])
r=load('Jump_Cute');lo,hi=r.animation_data.action.frame_range;frames=[]
for i in range(42):
 p,s=read(r,lo+(hi-lo)*i/41)
 for n in p:
  if n.endswith('.R') and any(x in n for x in ('Shoulder','Arm','Hand','Finger')):p[n]=carry[n].copy()
 frames.append((p,s))
export(r,'Carry_Jump',frames)
for prefix in ('Crawl_','Carry_Crawl_'):
 for direction in ('Forward','Back','Left','Right'):
  name=prefix+direction;r=load(name);lo,hi=r.animation_data.action.frame_range;duration=hi-lo;frames=[]
  for i in range(37):
   p,s=read(r,lo+duration*i/36)
   for side in ('R','L'):
    offset=(0 if side=='R' else .5)+( .5 if direction=='Back' else 0)
    phase=(i/36-offset+.075)%1
    warped=.5*phase/.65 if phase<.65 else .5+.5*(phase-.65)/.35
    q,z=read(r,lo+duration*((warped+offset)%1))
    for n in p:
     if n.endswith('.'+side) and any(x in n for x in ('UpperLeg','Leg.','Foot')):p[n]=q[n]
    for n in s:
     if n.endswith('_'+side) and n.startswith('Crawl_Follow'):s[n]=z[n]
   frames.append((p,s))
  frames[-1]=frames[0];export(r,name,frames)
