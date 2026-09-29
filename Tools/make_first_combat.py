"""Cute right punch, hit reaction, and a stagger-to-slump knockout."""
import bpy,sys,math
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
args=sys.argv[sys.argv.index('--')+1:];root,refs,out=map(Path,args[:3]);only=args[3] if len(args)>3 else None;out.mkdir(parents=True,exist_ok=True)
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path));r=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');bpy.context.scene.frame_set(int(r.animation_data.action.frame_range[0]));bpy.context.view_layer.update();return r
def pose(r):return {b.name:b.matrix_basis.copy() for b in r.pose.bones}
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def mix(a,b,t):
 al,aq,asc=a.decompose();bl,bq,bsc=b.decompose();return Matrix.LocRotScale(al.lerp(bl,t),aq.slerp(bq,t),asc.lerp(bsc,t))
samples={}
for name,frames in [('Punching',[1,10,16,23]),('Big Hit To Head',[1,9,17,29]),('Knocked Out',[25,45,65,85])]:
 r=load(refs/(name+'.fbx'));samples[name]=[]
 for f in frames:bpy.context.scene.frame_set(f);bpy.context.view_layer.update();samples[name].append(pose(r))
r=load(root/'FirstPlayerCapsule_Crouch_Idle.fbx');crouch=pose(r)
for name,ref,last in [('Punch','Punching',24),('Hit','Big Hit To Head',30),('Stun_Start','Knocked Out',66)]:
 if only and name!=only:continue
 r=load(root/'FirstPlayerCapsule_Idle.fbx');idle=pose(r);keys=bpy.data.objects['Body'].data.shape_keys
 targets=[]
 for source in samples[ref]:
  target={n:m.copy() for n,m in idle.items()}
  for n in target:
   arm=any(x in n for x in ('Shoulder','Arm','Hand','Finger'))
   if name=='Punch':
    weight=(1 if arm and n.endswith('.R') else .25 if n in ('Spine','Neck','Head') else 0)
   elif name=='Hit':
    weight=(.55 if arm else .6 if n in ('Spine','Neck','Head') else 0)
   else:
    # Keep First arms off the belly. Mixamo fold drives them through the torso.
    weight=(0 if arm else .6 if n in ('Spine','Neck','Head') else 0)
   if weight and n in source:
    loc,q,sc=idle[n].decompose();target[n]=Matrix.LocRotScale(loc,q.slerp(source[n].to_quaternion(),weight),sc)
  targets.append(target)
 if name=='Punch':
  # A clear forward extension, retaining the reference wind-up.
  for bone in r.pose.bones:bone.matrix_basis=targets[2][bone.name]
  bpy.context.view_layer.update()
  for bn,d in [('UpperArm.R',Vector((-.20,.08,1))),('Arm.R',Vector((-.10,.08,1)))]:
   bone=r.pose.bones[bn];q=(bone.tail-bone.head).normalized().rotation_difference(d.normalized());bone.matrix=Matrix.LocRotScale(bone.head,q@bone.matrix.to_quaternion(),bone.matrix.to_scale());bpy.context.view_layer.update()
  targets[2]=pose(r)
  points=[(0,idle),(6,targets[1]),(10,targets[2]),(13,targets[2]),(24,idle)]
 elif name=='Hit':points=[(0,idle),(5,targets[1]),(11,targets[2]),(20,targets[3]),(30,idle)]
 else:
  slump={n:m.copy() for n,m in idle.items()}
  loc,q,sc=slump['Hips'].decompose();loc.y-=.15;loc.z-=.25;slump['Hips']=Matrix.LocRotScale(loc,Quaternion((1,0,0),-math.pi/2)@q,sc)
  for bone in r.pose.bones:
   bone.rotation_mode='QUATERNION';bone.matrix_basis=slump[bone.name]
  bpy.context.view_layer.update()
  def apply_world_rotation(bone,world_rot):
   axes=bone.matrix.to_3x3();local=axes.inverted()@world_rot.to_matrix()@axes
   bone.matrix_basis=bone.matrix_basis@local.to_4x4()
  def aim(bn,d):
   bone=r.pose.bones[bn];current=bone.tail-bone.head
   apply_world_rotation(bone,current.normalized().rotation_difference(d.normalized()))
   bpy.context.view_layer.update()
  # Bake space is Y-up. After the back-fall, the head is -Z and the floor is -Y.
  aim('UpperArm.L',Vector((.80,-.40,.45)));aim('Arm.L',Vector((.40,-.35,.85)));aim('Hand.L',Vector((.20,-.20,.96)))
  aim('UpperArm.R',Vector((-.80,-.40,.45)));aim('Arm.R',Vector((-.40,-.35,.85)));aim('Hand.R',Vector((-.20,-.20,.96)))
  # Soft knee bend, feet turned out, shins droop toward the floor.
  aim('UpperLeg.L',Vector((.20,-.16,.97)));aim('Leg.L',Vector((.16,-.36,.92)));aim('Foot.L',Vector((.38,-.30,.88)))
  aim('UpperLeg.R',Vector((-.20,-.16,.97)));aim('Leg.R',Vector((-.16,-.36,.92)));aim('Foot.R',Vector((-.38,-.30,.88)))
  slump=pose(r)
  points=[(0,idle),(10,targets[0]),(20,targets[1]),(30,targets[2]),(44,slump),(66,slump)]
 r.animation_data.action=bpy.data.actions.new(name);keys.animation_data_clear()
 for f in range(last+1):
  for (fa,a),(fb,b) in zip(points,points[1:]):
   if fa<=f<=fb:t=smooth((f-fa)/(fb-fa));break
  for bone in r.pose.bones:bone.matrix_basis=mix(a[bone.name],b[bone.name],t);bone.rotation_mode='QUATERNION'
  bpy.context.view_layer.update()
  if name=='Stun_Start' and f>=30:
   # Ground the full skin, including the large hood, rather than the feet.
   deps=bpy.context.evaluated_depsgraph_get();minimum=100
   for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    ev=obj.evaluated_get(deps);mesh=ev.to_mesh();m=r.matrix_world.inverted()@obj.matrix_world
    minimum=min(minimum,min((m@v.co).y for v in mesh.vertices));ev.to_mesh_clear()
   r.pose.bones['Hips'].location.y-=minimum*smooth((f-30)/14);bpy.context.view_layer.update()
  if name=='Punch':
   h=r.pose.bones['Hand.R'];l=r.pose.bones['Arm.R'];q=(h.tail-h.head).normalized().rotation_difference((l.tail-l.head).normalized());original=h.matrix.copy();h.matrix=Matrix.LocRotScale(h.head,q@h.matrix.to_quaternion(),h.matrix.to_scale());h.matrix=mix(original,h.matrix,smooth(f/3)*smooth((last-f)/5))
  for bone in r.pose.bones:
   for prop in ('location','rotation_quaternion','scale'):bone.keyframe_insert(prop,frame=f)
  for k in keys.key_blocks[1:]:k.value=0;k.keyframe_insert('value',frame=f)
 s=bpy.context.scene;s.render.fps=30;s.frame_start=0;s.frame_end=last;s.frame_set(0)
 bpy.ops.export_scene.fbx(filepath=str(out/f'FirstPlayerCapsule_{name}.fbx'),use_selection=False,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',primary_bone_axis='Y',secondary_bone_axis='X',mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(name+'.blend')))
 print('BAKED',name,last)
