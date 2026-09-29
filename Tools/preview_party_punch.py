"""Create a separate Party Animals inspired right-hand punch for review."""
import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
ROOT=Path('C:/Users/SSAFY/barleymilk/S15P21D205/Assets/Scenes/CharacterTest/First')
OUT=Path('C:/Users/SSAFY/.codex/visualizations/2026/09/07/01a0792f-184a-72c2-8704-dfc1b0ae814d/party_punch')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'FirstPlayerCapsule_Punch.fbx'))
s=bpy.context.scene;r=next(o for o in s.objects if o.type=='ARMATURE')
start=r.animation_data.action.frame_range[0]
def pose():return {b.name:b.matrix_basis.copy() for b in r.pose.bones}
s.frame_set(int(start));idle=pose()
s.frame_set(int(start+10));fist=pose()
r.animation_data.action=bpy.data.actions.new('Punch_Party_Review')
def apply(p):
 for b in r.pose.bones:b.rotation_mode='QUATERNION';b.matrix_basis=p[b.name]
 bpy.context.view_layer.update()
def aim(n,d):
 b=r.pose.bones[n];q=(b.tail-b.head).normalized().rotation_difference(Vector(d).normalized())
 b.matrix=Matrix.LocRotScale(b.head,q@b.matrix.to_quaternion(),b.matrix.to_scale());bpy.context.view_layer.update()
def target(upper,lower,yaw,lean):
 apply(idle)
 b=r.pose.bones['Spine'];l,q,sc=b.matrix_basis.decompose();b.matrix_basis=Matrix.LocRotScale(l,Quaternion((0,1,0),yaw)@Quaternion((1,0,0),lean)@q,sc)
 bpy.context.view_layer.update()
 aim('UpperArm.R',upper);aim('Arm.R',lower);aim('Hand.R',lower)
 for b in r.pose.bones:
  if b.name.startswith('Finger') and b.name.endswith('.R'):b.matrix_basis=fist[b.name]
 bpy.context.view_layer.update();return pose()
wind=target((-.85,.03,-.52),(-.22,.65,.73),-.16,-.045)
strike=target((-.3,.18,.94),(-.02,.20,1),.17,.055)
follow=target((-.20,.12,1),(.12,.12,1),.20,.07)
points=[(0,idle),(7,wind),(10,strike),(12,follow),(24,idle)]
def mix(a,b,t):
 l,q,sc=a.decompose();ll,qq,ss=b.decompose();return Matrix.LocRotScale(l.lerp(ll,t),q.slerp(qq,t),sc.lerp(ss,t))
for f in range(25):
 for (fa,a),(fb,b) in zip(points,points[1:]):
  if fa<=f<=fb:break
 t=(f-fa)/(fb-fa);t=t*t*(3-2*t)
 apply({n:mix(a[n],b[n],t) for n in a})
 for bone in r.pose.bones:
  for prop in ('location','rotation_quaternion','scale'):bone.keyframe_insert(prop,frame=f)
s.render.fps=30;s.frame_start=0;s.frame_end=24;s.frame_set(0)
bpy.ops.export_scene.fbx(filepath=str(OUT/'FirstPlayerCapsule_Punch.fbx'),use_selection=False,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.65,.72,.8)
s.render.resolution_x=600;s.render.resolution_y=600;s.render.resolution_percentage=100
c=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera'));s.collection.objects.link(c);s.camera=c;c.data.type='ORTHO';c.data.ortho_scale=2.55
targetpos=r.matrix_world@Vector((0,.95,0));c.location=r.matrix_world@Vector((-2,1.5,3));c.rotation_euler=(targetpos-c.location).to_track_quat('-Z','Y').to_euler()
previous=None;maxangle=0
for f in range(25):
 s.frame_set(f);bpy.context.view_layer.update();qs=[b.matrix.to_quaternion() for b in r.pose.bones]
 if previous:maxangle=max(maxangle,max(2*math.acos(min(1,abs(a.dot(b)))) for a,b in zip(qs,previous)))
 previous=qs
 for b in r.pose.bones:assert abs((b.tail-b.head).length-b.bone.length)<1e-4
 if f%2==0:
  s.render.filepath=str(OUT/f'punch_{f:02}.png');bpy.ops.render.render(write_still=True)
assert maxangle<math.radians(65),maxangle
s.frame_set(7);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Punch_Party_Review.blend'))
print('PASS constant bone lengths; maximum frame rotation',math.degrees(maxangle))
