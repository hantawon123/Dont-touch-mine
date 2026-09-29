"""Build rounded hip joins and intermediate bend bones for Unity skinning."""
import bpy,sys,json,shutil,math,numpy as np
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from smooth_bear_skinning import capture_weights
from hold_crawl_torso import FBX_KW
from hold_crawl_torso import skin_from_names
OUT=ROOT/'artifacts/smooth-bear-unity/joint-volume';OUT.mkdir(exist_ok=True)
SOURCE=Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/supplied-bear-smooth/BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
source_weights=capture_weights(bpy.data.objects['Body'])
author=ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'
model=ROOT/'Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx'
for p in [author,model]:
    if not (OUT/('before-'+p.name)).exists():shutil.copy2(p,OUT/('before-'+p.name))
bpy.ops.wm.open_mainfile(filepath=str(OUT/'before-SmoothBear.blend'),use_scripts=False)
body=bpy.data.objects['Body'];rig=bpy.data.objects['DGN_Armature'];rig.animation_data_clear()
scene=bpy.context.scene
motions={}
for name in ('Idle','Crawl_Forward','Crouch_Idle','Carry_TwoHands'):
    existing=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/f'Temp/smooth-bear-unity-qa/Assets/Source/FirstPlayerCapsule_{name}.fbx'))
    added=set(bpy.data.objects)-existing;motion=next(o for o in added if o.type=='ARMATURE')
    scene.frame_set(10);bpy.context.view_layer.update()
    motions[name]={b.name:b.matrix_basis.copy() for b in motion.pose.bones}
    for obj in added:bpy.data.objects.remove(obj,do_unlink=True)
scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO';scene.display.shading.color_type='SINGLE';scene.display.shading.single_color=(.8,.74,.42)
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
cam=bpy.data.objects.new('JointCamera',bpy.data.cameras.new('JointCamera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.4
pairs=[]
def pose(name):
    rig.data.pose_position='POSE'
    for b in rig.pose.bones:b.matrix_basis.identity()
    for n,m in motions[name].items():rig.pose.bones[n].matrix_basis=m
    for child,helper,parent in pairs:
        loc,rot,scale=rig.pose.bones[child].matrix_basis.decompose()
        h=rig.pose.bones[helper];h.rotation_mode='QUATERNION';h.location=loc
        h.rotation_quaternion=Quaternion().slerp(rot,.5);h.scale=scale
    bpy.context.view_layer.update()
def render(label,name):
    pose(name)
    target=Vector((0,0,1.0)) if name!='Crawl_Forward' else Vector((0,0,.65))
    cam.location=(0,-5,1.0) if name!='Crawl_Forward' else (3,-4,2.0)
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'{label}-{name}.png');bpy.ops.render.render(write_still=True)
def skin_matrices():
    return {g.index:body.matrix_world.inverted()@rig.matrix_world@rig.pose.bones[g.name].matrix@rig.data.bones[g.name].matrix_local.inverted()@rig.matrix_world.inverted()@body.matrix_world
            for g in body.vertex_groups if g.name in rig.pose.bones}
def thickness():
    pose('Crawl_Forward');matrices=skin_matrices();values={}
    for region,prefixes in [('arms',('UpperArm.','Arm.','Volume_UpperArm.','Volume_Arm.')),('legs',('UpperLeg.','Leg.','Volume_UpperLeg.','Volume_Leg.'))]:
        samples=[]
        for v in body.data.vertices:
            if sum(g.weight for g in v.groups if body.vertex_groups[g.group].name.startswith(prefixes))>.65:
                m=skin_from_names(v,body,matrices,None)
                samples.append(np.linalg.svd(np.array(m)[:3,:3],compute_uv=False)[-1])
        values[region]={'minimum':float(min(samples)),'p10':float(np.percentile(samples,10)),'median':float(np.median(samples))}
    return values
metrics={'before':thickness()}
for n in motions:render('before',n)
# Restore a soft pelvis-to-thigh join instead of the old rigid Spine mask.
for v,source in zip(body.data.vertices,source_weights):
    t=max(0,min(1,(.72-v.co.y)/.15));t=t*t*(3-2*t)
    if not t:continue
    old={body.vertex_groups[g.group].name:g.weight for g in v.groups}
    for g in body.vertex_groups:g.remove([v.index])
    for n in old.keys()|source.keys():
        w=old.get(n,0)*(1-t)+source.get(n,0)*t
        if w>1e-9:body.vertex_groups[n].add([v.index],w,'REPLACE')
for n in ('Idle','Crawl_Forward'):render('pelvis',n)
# Hold only the middle of the torso; the hip and armpit joins remain articulated.
for v in body.data.vertices:
    t=max(0,min(1,(.28-abs(v.co.x))/.08))*max(0,min(1,(v.co.y-.70)/.10))*max(0,min(1,(1.30-v.co.y)/.14))
    t=t*t*(3-2*t)
    if t==0:continue
    total=0
    for g in list(v.groups):
        group=body.vertex_groups[g.group]
        if group.name=='Spine':continue
        amount=g.weight*t;group.add([v.index],g.weight-amount,'REPLACE');total+=amount
    body.vertex_groups['Spine'].add([v.index],total,'ADD')
rig.data.pose_position='REST';bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for side in ('L','R'):
    for child in ('UpperArm.'+side,'Arm.'+side,'UpperLeg.'+side,'Leg.'+side):
        bone=rig.data.edit_bones[child];helper='Volume_'+child
        support=rig.data.edit_bones.new(helper);support.head=bone.head;support.tail=bone.tail;support.roll=bone.roll;support.parent=bone.parent;support.use_deform=True
        pairs.append((child,helper,bone.parent.name))
bpy.ops.object.mode_set(mode='OBJECT')
for child,helper,parent in pairs:body.vertex_groups.new(name=helper)
for v in body.data.vertices:
    weights={body.vertex_groups[g.group].name:g.weight for g in v.groups}
    for child,helper,parent in pairs:
        amount=min(weights.get(child,0),weights.get(parent,0))
        if amount<1e-6:continue
        weights[child]-=amount;weights[parent]-=amount;weights[helper]=2*amount
    for g in body.vertex_groups:g.remove([v.index])
    for n,w in weights.items():
        if w>1e-8:body.vertex_groups[n].add([v.index],w,'REPLACE')
for n in motions:render('support',n)
# Smooth the hip join in the actual standing pose and transform that small
# correction back into bind space. Taubin passes retain the pelvis volume.
pose('Idle')
evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
coords=np.array([v.co[:] for v in mesh.vertices],dtype=np.float64);normals=np.array([v.normal[:] for v in mesh.vertices],dtype=np.float64);evaluated.to_mesh_clear();original=coords.copy()
edges=np.array([e.vertices[:] for e in body.data.edges]);a=np.concatenate((edges[:,0],edges[:,1]));b=np.concatenate((edges[:,1],edges[:,0]));counts=np.bincount(a,minlength=len(coords))
y=np.array([v.co.y for v in body.data.vertices]);x=np.array([abs(v.co.x) for v in body.data.vertices]);mask=np.clip((y-.30)/.16,0,1)*np.clip((.90-y)/.18,0,1)*np.clip((.48-x)/.12,0,1);mask=mask*mask*(3-2*mask)
for _ in range(100):
    for rate in (.5,-.53):
        avg=np.zeros_like(coords)
        for axis in range(3):avg[:,axis]=np.bincount(a,weights=coords[b,axis],minlength=len(coords))/counts
        coords+=(avg-coords)*(rate*mask[:,None])
for _ in range(180):
    avg=np.zeros_like(coords)
    for axis in range(3):avg[:,axis]=np.bincount(a,weights=coords[b,axis],minlength=len(coords))/counts
    lap=avg-coords;normal_component=np.sum(lap*normals,axis=1)
    # Fill inward dents while retaining the outer, rounded hip silhouette.
    tangent=lap-normal_component[:,None]*normals
    coords+=(.35*tangent+.65*np.maximum(normal_component,0)[:,None]*normals)*mask[:,None]
matrices=skin_matrices();largest=0
for v,delta in zip(body.data.vertices,coords-original):
    if np.linalg.norm(delta)<1e-7:continue
    m=skin_from_names(v,body,matrices,None).to_3x3()
    correction=m.inverted_safe()@Vector(delta.tolist());largest=max(largest,correction.length)
    for key in body.data.shape_keys.key_blocks:key.data[v.index].co+=correction
for n in motions:render('rounded',n)
metrics['after']=thickness();metrics['max_hip_correction']=largest
(OUT/'thickness-report.json').write_text(json.dumps(metrics,indent=2))
rig.data.pose_position='POSE'
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects:
    if obj.type=='MESH' or obj==rig:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
kwargs=FBX_KW.copy();kwargs.update(bake_anim=False,embed_textures=False,path_mode='AUTO')
bpy.ops.export_scene.fbx(filepath=str(OUT/'SmoothBear.fbx'),**kwargs)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SmoothBear.blend'),check_existing=False)
(OUT/'support-bones.json').write_text(json.dumps(pairs,indent=2))
print('JOINT_VOLUME_READY',len(pairs),flush=True)
