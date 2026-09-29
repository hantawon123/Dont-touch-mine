"""Bake approved right punch, rest-space mirrored left punch and overlapping combos."""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion
ROOT=Path(__file__).resolve().parents[1]
FIRST=ROOT/'Assets/Scenes/CharacterTest/First'
OUT=Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else FIRST
SOURCE=FIRST/'FirstPlayerCapsule_Punch.fbx'
OUT.mkdir(exist_ok=True)
UPPER_PREFIX=('Shoulder','UpperArm','Arm','Hand','Finger')
UPPER_EXACT={'Spine','Neck','Head'}
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 if path.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(path),use_scripts=False)
 else:bpy.ops.import_scene.fbx(filepath=str(path))
 return next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
def sample(path):
 r=load(path);a=r.animation_data.action;frames=[]
 for f in range(round(a.frame_range[0]),round(a.frame_range[1])+1):
  bpy.context.scene.frame_set(f);bpy.context.view_layer.update()
  keys=bpy.data.objects['Body'].data.shape_keys
  frames.append(({b.name:b.matrix_basis.copy() for b in r.pose.bones},{k.name:k.value for k in keys.key_blocks[1:]} if keys else {}))
 return frames
def mix(a,b,t):
 l,q,sc=a.decompose();ll,qq,ss=b.decompose();return Matrix.LocRotScale(l.lerp(ll,t),q.slerp(qq,t),sc.lerp(ss,t))
def is_upper(name):
 return name in UPPER_EXACT or any(name==p or name.startswith(p+'.') or name.startswith(p+'_') for p in UPPER_PREFIX)

def grip_amount(frame):
 return min(1.0, frame/6.0, max(0.0,(24-frame)/6.0))

raw=sample(SOURCE)
idle_clip=sample(FIRST/'FirstPlayerCapsule_Idle.fbx')
idle_pose=idle_clip[0][0]
fist_src=ROOT/'Tools/_punch_fist_src.fbx'
fist_pose=sample(fist_src if fist_src.exists() else SOURCE)[min(10,len(raw)-1)][0]
fingers=[n for n in idle_pose if n.startswith('Finger') and n.endswith('.R')]
right=[]
for i,(pose,sh) in enumerate(raw):
 cleaned={n:m.copy() for n,m in idle_pose.items()}
 for n,m in pose.items():
  if is_upper(n) and not n.startswith('Finger'):cleaned[n]=m.copy()
 grip=grip_amount(i)
 for n in fingers:
  if n in fist_pose:cleaned[n]=mix(idle_pose[n],fist_pose[n],grip)
 right.append((cleaned,sh.copy()))
assert len(right)==25,len(right)
r=load(SOURCE);ordered=sorted(r.pose.bones,key=lambda b:len(b.parent_recursive));reflect=Matrix.Diagonal((-1,1,1,1));left=[]
for p,sh in right:
 for b in ordered:b.matrix_basis=p[b.name]
 bpy.context.view_layer.update();world={b.name:b.matrix.copy() for b in ordered}
 for b in ordered:
  n=b.name[:-2]+('.L' if b.name.endswith('.R') else '.R') if b.name.endswith(('.L','.R')) else b.name
  source=r.data.bones[n]
  b.matrix=reflect@world[n]@source.matrix_local.inverted()@reflect@b.bone.matrix_local
  bpy.context.view_layer.update()
 left.append(({b.name:b.matrix_basis.copy() for b in ordered},sh.copy()))
COMBO_OFFSET = 12
COMBO_LOOP = 24

def combo_index(phase):
 """Wind+strike in 12 frames, then recover while the other hand already starts."""
 phase %= COMBO_LOOP
 if phase < COMBO_OFFSET:
  return min(16, phase)
 t = (phase - COMBO_OFFSET) / max(1, COMBO_OFFSET - 1)
 return min(24, round(12 + t * 12))

idle=right[0][0]
def combo(f):
 rp=right[combo_index(f)][0];lp=left[combo_index(f-COMBO_OFFSET)][0];p={n:m.copy() for n,m in idle.items()}
 for n in p:
  if n.endswith('.R'):p[n]=rp[n]
  elif n.endswith('.L'):p[n]=lp[n]
  else:
   l,q,sc=idle[n].decompose();rl,rq,rs=rp[n].decompose();ll,lq,ls=lp[n].decompose()
   p[n]=Matrix.LocRotScale(l+(rl-l)+(ll-l),(rq@q.inverted())@(lq@q.inverted())@q,sc)
 return p

def curve(f,points):
 if f>=points[-1][0]:return points[-1][1]
 for (a,x),(b,y) in zip(points,points[1:]):
  if a<=f<=b:
   t=(f-a)/(b-a);t=t*t*(3-2*t);return x+(y-x)*t
 return points[0][1]

def footwork(r,f,family,suffix):
 """Right punch: sit into the far knee, lift the punching hip, extend that leg."""
 events=[(combo_index(f),1),(combo_index(f-COMBO_OFFSET),-1)] if family=='Punch_Combo' else [(f, -1 if family=='Punch_Left' else 1)]
 moving=suffix in ('_Walk','_Run','_Crouch_Walk')
 crouch='Crouch' in suffix
 strength=.35 if moving else .3 if crouch else 1.0
 sway=lift=forward=yaw=roll=0.0
 flex={'L':0.0,'R':0.0}
 for phase,side in events:
  support='L' if side>0 else 'R'
  # Weight onto the support (left on a right punch). +X is left.
  sway+=side*curve(phase,[(0,0),(7,-.004),(11,.02),(16,.01),(24,0)])
  # Sit down at impact so the support knee has to bend.
  lift+=curve(phase,[(0,0),(7,-.01),(11,-.028),(16,-.012),(24,0)])
  forward+=curve(phase,[(0,0),(7,-.006),(11,.014),(16,.008),(24,0)])
  yaw+=side*curve(phase,[(0,0),(7,-.08),(11,.10),(16,.05),(24,0)])
  # Negative world-Z roll lifts the punching (right) hip and drops the support hip.
  roll+=side*curve(phase,[(0,0),(7,.04),(11,-.13),(16,-.06),(24,0)])
  flex[support]+=curve(phase,[(0,0),(7,.25),(11,1),(16,.45),(24,0)])
 if abs(sway)+abs(lift)+abs(forward)+abs(yaw)+abs(roll)<1e-8:return 0.0
 feet={side:r.pose.bones['Foot.'+side].matrix.copy() for side in ('L','R')}
 knees={side:r.pose.bones['Leg.'+side].head.copy() for side in ('L','R')}
 hips=r.pose.bones['Hips'];loc,q,sc=hips.matrix.decompose()
 hips.matrix=Matrix.LocRotScale(
  loc+Vector((sway,lift,forward))*strength,
  Quaternion((0,0,1),roll*strength)@Quaternion((0,1,0),yaw*strength)@q,
  sc)
 bpy.context.view_layer.update();error=0.0
 for side in ('L','R'):
  upper=r.pose.bones['UpperLeg.'+side];lower=r.pose.bones['Leg.'+side];foot=r.pose.bones['Foot.'+side]
  start=upper.head.copy();target=feet[side].translation;axis=target-start;distance=axis.length;direction=axis.normalized()
  a=upper.bone.length;b=lower.bone.length
  reach=max(abs(a-b)+1e-5,min(a+b-1e-5,distance))
  pole=knees[side]-start;pole-=direction*pole.dot(direction)
  # Support knee pops forward so the bend reads; punching leg stays closer to the hip-foot line.
  pole+=Vector((0,0,0.12))*flex[side]*strength
  if pole.length<1e-5:pole=Vector((0,0,1))-direction*direction.z
  pole.normalize();along=(a*a-b*b+reach*reach)/(2*reach)
  knee=start+direction*along+pole*math.sqrt(max(0,a*a-along*along))
  ankle=start+direction*reach
  for bone,point in ((upper,knee),(lower,ankle)):
   rot=(bone.tail-bone.head).normalized().rotation_difference((point-bone.head).normalized())
   bone.matrix=Matrix.LocRotScale(bone.head,rot@bone.matrix.to_quaternion(),bone.matrix.to_scale());bpy.context.view_layer.update()
  foot.matrix=Matrix.LocRotScale(foot.head,feet[side].to_quaternion(),feet[side].to_scale());bpy.context.view_layer.update()
  error=max(error,(foot.head-target).length)
 return error
bases={suffix:sample(FIRST/f'FirstPlayerCapsule_{name}.fbx') for suffix,name in [('', 'Idle'),('_Walk','Walk_Forward'),('_Run','Run_Forward'),('_Crouch','Crouch_Idle'),('_Crouch_Walk','Crouch_Walk_Forward')]}
manifest=[]
for family,frames in [('Punch',right),('Punch_Left',left),('Punch_Combo',None)]:
 for suffix,base in bases.items():
  name=family+suffix;period=len(base)-1;iscombo=frames is None
  last=(math.lcm(COMBO_LOOP,period) if suffix in ('_Walk','_Run','_Crouch_Walk') else COMBO_LOOP) if iscombo else 24
  assert last<=360,(name,last)
  r=load(FIRST/'FirstPlayerCapsule_Idle.fbx');keys=bpy.data.objects['Body'].data.shape_keys;keys.animation_data_clear()
  r.animation_data.action=bpy.data.actions.new(name)
  previous=None;maxstep=0;firstpose=None;lastpose=None;foot_error=0;leg_rotations=[]
  for f in range(last+1):
   p=combo(f) if iscombo else frames[f][0]
   base_index=round(f*period/last)%period if iscombo and suffix in ('','_Crouch') else f%period
   bp,sh=base[base_index]
   envelope=1 if iscombo else min(1,f/3,(24-f)/5)
   for b in r.pose.bones:
    b.rotation_mode='QUATERNION';n=b.name
    arm=any(x in n for x in ('Shoulder','Arm','Hand','Finger'))
    puncharm=iscombo or n.endswith('.L' if family=='Punch_Left' else '.R')
    if not suffix:b.matrix_basis=p[n]
    elif arm and puncharm:b.matrix_basis=mix(bp[n],p[n],envelope)
    elif n in ('Spine','Neck','Head'):
     l,q,sc=bp[n].decompose();delta=p[n].to_quaternion()@idle[n].to_quaternion().inverted()
     b.matrix_basis=Matrix.LocRotScale(l,delta@q,sc)
    else:b.matrix_basis=bp[n]
   bpy.context.view_layer.update()
   foot_error=max(foot_error,footwork(r,f,family,suffix))
   leg_rotations.append(r.pose.bones['UpperLeg.L'].matrix.to_quaternion().copy())
   qs=[b.matrix.to_quaternion() for b in r.pose.bones]
   if previous:maxstep=max(maxstep,max(math.degrees(2*math.acos(min(1,abs(a.dot(b))))) for a,b in zip(previous,qs)))
   previous=qs
   now={b.name:b.matrix_basis.copy() for b in r.pose.bones}
   if f==0:firstpose=now
   lastpose=now
   for b in r.pose.bones:
    assert abs((b.tail-b.head).length-b.bone.length)<1e-4,(name,f,b.name)
    for prop in ('location','rotation_quaternion','scale'):b.keyframe_insert(prop,frame=f)
   for k in keys.key_blocks[1:]:
    k.value=sh.get(k.name,0);k.keyframe_insert('value',frame=f)
  assert maxstep<90,(name,maxstep)
  assert foot_error<.015,(name,'foot target error',foot_error)
  leg_motion=max(math.degrees(2*math.acos(min(1,abs(q.dot(leg_rotations[0]))))) for q in leg_rotations)
  assert leg_motion>3,(name,'static legs',leg_motion)
  if iscombo:
   for n in firstpose:assert max(abs(firstpose[n][i][j]-lastpose[n][i][j]) for i in range(4) for j in range(4))<1e-4,(name,n,'loop seam')
   # Neutral breathing shape follows its own cycle; ensure the combo boundary closes.
   for k in keys.key_blocks[1:]:k.value=base[0][1].get(k.name,0);k.keyframe_insert('value',frame=last)
  s=bpy.context.scene;s.render.fps=30;s.frame_start=0;s.frame_end=last;s.frame_set(0)
  bpy.ops.export_scene.fbx(filepath=str(OUT/f'FirstPlayerCapsule_{name}.fbx'),use_selection=False,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
  if OUT!=FIRST:bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{name}.blend'))
  manifest.append(dict(name=name,last=last,loop=iscombo,maxStep=maxstep,footTargetError=foot_error,legMotionDegrees=leg_motion))
  print('PASS',name,last,'maxStep',round(maxstep,2),'leg',round(leg_motion,2),'foot',round(foot_error,4),flush=True)
if OUT!=FIRST:(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
