"""Create a separate editable sparkle-eye expression on the approved shoe character."""
import bpy, math, json, hashlib, struct
from pathlib import Path
from mathutils import Vector, Quaternion
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/sparkle';OUT.mkdir(parents=True,exist_ok=True)
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions';DEST.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_SparkleEyes.blend'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/SmoothBear/Shoes/BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature']
def fingerprint(obj):
    return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
preserved={name:fingerprint(bpy.data.objects[name]) for name in ['Body','Shoes.L','Shoes.R','Hood']}
rig.data.pose_position='REST';bpy.context.view_layer.update()
collection=bpy.data.collections.new('EXPRESSION - Sparkle Eyes');scene.collection.children.link(collection)
def color(h):
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in [int(h[i:i+2],16)/255 for i in (1,3,5)])+(1,)
def material(name,h,roughness,emission=0):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=color(h)
    node=mat.node_tree.nodes.get('Principled BSDF');node.inputs['Base Color'].default_value=color(h);node.inputs['Roughness'].default_value=roughness
    node.inputs['Emission Color'].default_value=color(h);node.inputs['Emission Strength'].default_value=emission
    return mat
black=material('MAT_Eyes_Sparkle_Black','#12121C',.23)
star_mat=material('MAT_Eyes_Sparkle_Stars','#FFF3A1',.4,.45)
shine_mat=material('MAT_Eyes_Sparkle_Glints','#FFFDF2',.3,.35)
liner_mat=material('MAT_Eyes_Sparkle_Liner','#23232B',.6)
created=[]
def finish(obj,name,mat):
    obj.name=name
    # Bake world coordinates before the existing Head bone deforms the expression.
    bpy.context.view_layer.update()
    transform=obj.matrix_world.copy()
    obj.data.transform(transform);obj.matrix_world.identity()
    for col in list(obj.users_collection):col.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear();obj.data.materials.append(mat)
    for p in obj.data.polygons:p.use_smooth=True
    obj.vertex_groups.new(name='Head').add(list(range(len(obj.data.vertices))),1,'REPLACE')
    mod=obj.modifiers.new('Follow Head','ARMATURE');mod.object=rig
    obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted()
    obj['expression']='Sparkle';created.append(obj)
    return obj
def ellipsoid(name,center,radii,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=32,location=center)
    obj=bpy.context.object;obj.scale=radii
    obj=finish(obj,name,mat)
    for k in range(3):
        span=max(v.co[k] for v in obj.data.vertices)-min(v.co[k] for v in obj.data.vertices)
        assert abs(span-2*radii[k])<.001, (name,k,span,radii)
    return obj
rx,rz,depth=.074,.080,.035
for side in ['L','R']:
    original=bpy.data.objects['Eye_Pupil_'+side];original.hide_render=True;original.hide_set(True)
    original['expression']='Default (hidden while Sparkle is active)'
    white=bpy.data.objects['Eye_White_'+side]
    cx=white.matrix_world.translation.x;cz=white.matrix_world.translation.z+.002
    cy=white.matrix_world.translation.y-.046
    ellipsoid('Eye_Sparkle_Pupil_'+side,(cx,cy,cz),(rx,depth,rz),black)
    def surface(dx,dz):
        return cy-depth*math.sqrt(max(.03,1-(dx/rx)**2-(dz/rz)**2))
    def star(name,ox,oz,sx,sz):
        # Astroid outline: four pointed rays joined by soft concave curves.
        n=64;outline=[]
        for i in range(n):
            t=math.tau*i/n;dx=ox+sx*math.cos(t)**3;dz=oz+sz*math.sin(t)**3
            outline.append((cx+dx,surface(dx,dz)-.0018,cz+dz))
        verts=[(cx+ox,surface(ox,oz)-.0022,cz+oz)]+outline
        verts += [(x,y+.0015,z) for x,y,z in verts]
        faces=[]
        for i in range(n):
            a=1+i;b=1+(i+1)%n
            faces.extend([(0,a,b),(n+1,n+1+b,n+1+a),(a,n+1+a,n+1+b,b)])
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
        finish(obj,name,star_mat)
    star('Eye_Sparkle_StarLarge_'+side,-.015,.014,.033,.043)
    star('Eye_Sparkle_StarSmall_'+side,.031,-.030,.015,.020)
    for suffix,dx,dz,sx,sz in [('Upper',.035,.048,.010,.013),('Lower',-.041,-.044,.0045,.006)]:
        ellipsoid('Eye_Sparkle_Glint'+suffix+'_'+side,(cx+dx,surface(dx,dz)-.0018,cz+dz),(sx,.003,sz),shine_mat)
    # Thin curved top accent echoing the reference's animated eye outline.
    curve=bpy.data.curves.new('Eye_Sparkle_UpperLid_'+side,'CURVE');curve.dimensions='3D';curve.resolution_u=16
    curve.bevel_depth=.0035;curve.bevel_resolution=3
    spline=curve.splines.new('POLY');spline.points.add(40)
    for i,p in enumerate(spline.points):
        angle=math.radians(22+136*i/40)
        dx=.096*math.cos(angle);dz=.098*math.sin(angle)
        p.co=(cx+dx,white.matrix_world.translation.y-.026,cz+dz,1)
    obj=bpy.data.objects.new(curve.name,curve);scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH');finish(bpy.context.object,'Eye_Sparkle_UpperLid_'+side,liner_mat)
rig.data.pose_position='POSE';bpy.context.view_layer.update()
assert all(fingerprint(bpy.data.objects[n])==v for n,v in preserved.items())
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24
cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('sparkle-eyes-closeup',(0,-5,1.88),(0,0,1.64),.99,1050,950)
render('sparkle-eyes-character',(1.1,-7,2.5),(0,0,1.04),2.55,850,1100)
bpy.ops.object.select_all(action='DESELECT')
for obj in created:obj.select_set(True)
bpy.context.view_layer.objects.active=created[0]
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL'
            space.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)
            space.region_3d.view_location=(0,0,1.57);space.region_3d.view_distance=1.7
readme=bpy.data.texts.new('SPARKLE_EYES_README')
readme.write('Sparkle expression: EXPRESSION - Sparkle Eyes collection.\nLarge black pupils, two pale yellow four-point stars and white glints per eye.\nOriginal Eye_Pupil_L/R are hidden; the white eyeballs are shared.\nTo restore default eyes, hide the Sparkle collection in viewport AND render, then show original pupils.\nAll added meshes follow Head. Body, hood and approved compact shoes are unchanged.\nThis is a Blender expression draft; Unity has not been changed.\n')
for text in bpy.data.texts:text.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
report={'file':str(FILE),'expression':'Sparkle','objects':[obj.name for obj in created],'body_hood_approved_shoes_unchanged':True,'head_bone':'Head','original_pupils_preserved_hidden':True}
(OUT/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SPARKLE_EYES_SAVED '+str(FILE),flush=True)
