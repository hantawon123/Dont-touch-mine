"""Author separate, softly rounded slip-ons for the current character."""
import bpy,math,json
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/shoes';OUT.mkdir(parents=True,exist_ok=True)
DEST=ROOT/'source/blender/characters/SmoothBear/Shoes';DEST.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];body=bpy.data.objects['Body']
rig.animation_data_clear();rig.data.pose_position='REST'
for bone in rig.pose.bones:bone.matrix_basis.identity()
for key in body.data.shape_keys.key_blocks:key.value=0
body.data.shape_keys.animation_data_clear()
bpy.context.view_layer.update()
for text in bpy.data.texts:text.use_module=False
for obj in list(bpy.data.objects):
    if obj.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(obj,do_unlink=True)

def linear_hex(value):
    rgb=[int(value[i:i+2],16)/255 for i in (1,3,5)]
    return tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in rgb)+(1,)
def material(name,color):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=linear_hex(color)
    node=mat.node_tree.nodes.get('Principled BSDF');node.inputs['Base Color'].default_value=mat.diffuse_color
    node.inputs['Roughness'].default_value=.62
    return mat
upper=material('MAT_Shoes_Color','#789BC8');sole=material('MAT_Shoes_Sole','#6586B3')
shoes_collection=bpy.data.collections.new('CUSTOMIZATION - Shoes');scene.collection.children.link(shoes_collection)
# A continuous quad shell: rounded sole, padded upper, narrow lip, inner cavity.
# cx, cy, rx, ry, z. Feet point toward -Y.
profile=[(.180,-.080,.143,.207,-.026),(.180,-.080,.162,.229,-.024),
         (.180,-.080,.170,.238,-.009),(.180,-.080,.173,.241,.016),
         (.179,-.076,.173,.237,.065),(.174,-.062,.166,.214,.140),
         (.167,-.035,.149,.164,.211),(.163,-.018,.139,.136,.243),
         (.163,-.018,.138,.135,.250),(.163,-.018,.125,.122,.250),
         (.163,-.018,.123,.120,.238),(.165,-.021,.123,.122,.173),
         (.177,-.069,.145,.197,.007),(.177,-.069,.132,.184,.006)]
shoes=[];N=64
for side,sign in [('L',1),('R',-1)]:
    vertices=[];faces=[]
    for cx,cy,rx,ry,z in profile:
        for i in range(N):
            a=math.tau*i/N;c=math.cos(a);s=math.sin(a)
            # A soft superellipse gives the broad, rounded reference toe.
            vertices.append((sign*cx+rx*math.copysign(abs(c)**.88,c),cy+ry*math.copysign(abs(s)**.88,s),z))
    faces.append(tuple(reversed(range(N))))
    for j in range(len(profile)-1):
        for i in range(N):faces.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
    faces.append(tuple((len(profile)-1)*N+i for i in range(N)))
    mesh=bpy.data.meshes.new('SoftSlipOn_'+side);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new('Shoes.'+side,mesh);shoes_collection.objects.link(obj)
    obj.data.materials.append(upper);obj.data.materials.append(sole)
    for polygon in mesh.polygons:
        polygon.use_smooth=True
        polygon.material_index=1 if polygon.index<=N*2 else 0
    group=obj.vertex_groups.new(name='Foot.'+side);group.add(list(range(len(vertices))),1,'REPLACE')
    subdiv=obj.modifiers.new('Smooth padded silhouette','SUBSURF');subdiv.levels=2;subdiv.render_levels=2
    arm=obj.modifiers.new('Follow Foot.'+side,'ARMATURE');arm.object=rig
    obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted()
    obj['customization_slot']='Shoes';obj['color_material']='MAT_Shoes_Color';obj['design']='Rounded slip-on; hollow ankle opening; separate left/right mesh'
    shoes.append(obj)

# Display on the established standing pose; keep body geometry untouched.
poses={}
for name in ['Idle','Walk_Forward','Crawl_Forward']:
    existing=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/f'Temp/smooth-bear-unity-qa/Assets/Source/FirstPlayerCapsule_{name}.fbx'))
    added=set(bpy.data.objects)-existing;motion=next(o for o in added if o.type=='ARMATURE')
    scene.frame_set(10);bpy.context.view_layer.update()
    poses[name]={bone.name:bone.matrix_basis.copy() for bone in motion.pose.bones}
    for obj in added:bpy.data.objects.remove(obj,do_unlink=True)
def pose(name):
    rig.data.pose_position='POSE'
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    for key,value in poses[name].items():
        if key in rig.pose.bones:rig.pose.bones[key].matrix_basis=value
    for bone in rig.pose.bones:
        if bone.name.startswith('Volume_'):
            child=rig.pose.bones.get(bone.name[7:])
            if child:
                loc,rot,scale=child.matrix_basis.decompose();bone.rotation_mode='QUATERNION'
                bone.location=loc;bone.rotation_quaternion=Quaternion().slerp(rot,.5);bone.scale=scale
    bpy.context.view_layer.update()

studio=bpy.data.collections.new('PREVIEW - Studio');scene.collection.children.link(studio)
def track(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('Shoes_Preview_Camera',bpy.data.cameras.new('Shoes_Preview_Camera'));studio.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO'
def light(name,pos,power,size):
    obj=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));studio.objects.link(obj)
    obj.location=pos;obj.data.energy=power;obj.data.shape='DISK';obj.data.size=size;track(obj,(0,0,.9))
light('Softbox_Key',(-3,-4,5),400,4);light('Softbox_Fill',(3,-2,3),220,3);light('Softbox_Rim',(0,3,4),350,3)
ground_mat=material('Preview_Backdrop','#ECE5D8')
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.035));ground=bpy.context.object;ground.name='Preview_Ground';ground.data.materials.append(ground_mat)
for col in list(ground.users_collection):col.objects.unlink(ground)
studio.objects.link(ground)
scene.world.color=(.25,.25,.25)
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
def render(name,position,target,scale,width=1000,height=1000):
    cam.location=position;track(cam,target);cam.data.ortho_scale=scale
    scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
pose('Idle')
render('shoes-character-front',(0,-5,1.15),(0,0,.85),2.25,900,1100)
render('shoes-detail',(1.3,-2.7,1.0),(0,-.055,.12),.96,1100,850)
render('shoes-side',(3,-.7,.55),(0,-.065,.16),.95,1000,800)
pose('Walk_Forward');render('shoes-walk',(2,-4,1.3),(0,0,.78),2.15,900,1100)
pose('Crawl_Forward');render('shoes-crawl',(2.5,-3,1.5),(0,0,.6),2.2,1000,850)
pose('Idle')
cam.location=(2.5,-5,2.3);track(cam,(0,0,.8));cam.data.ortho_scale=2.1
scene.render.resolution_x=1000;scene.render.resolution_y=1000
for obj in studio.objects:obj.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
for obj in shoes:obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=shoes[0]
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL';region=area.spaces.active.region_3d
            region.view_distance=2.8;region.view_location=(0,0,.85);region.view_rotation=Quaternion((1,0,0),math.pi/2)
readme=bpy.data.texts.new('SHOES_README')
readme.write('Rounded slip-on shoes / reference study\nShoes.L and Shoes.R are separate, closed quad shells with real ankle cavities.\nMAT_Shoes_Color: #789BC8. MAT_Shoes_Sole: #6586B3.\nBoth follow the corresponding Foot bone. Subdivision remains editable.\nThe original character body mesh is unchanged. The opening is fitted for the standing pose.\nStudio collection is hidden in the viewport; enable it for preview rendering.\nUnity assets have not been replaced in this Blender-only draft.\n')
path=DEST/'BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(path))
report={'file':str(path),'shoe_objects':[o.name for o in shoes],'base_vertices_per_shoe':len(shoes[0].data.vertices),'base_faces_per_shoe':len(shoes[0].data.polygons),'preview_poses':list(poses),'upper_color':'#789BC8','sole_color':'#6586B3'}
(OUT/'report.json').write_text(json.dumps(report,indent=2));print('SHOES_SAVED '+str(path),flush=True)
