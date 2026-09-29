"""Soft drooping lids and glossy slate irises printed on the approved eye surface."""
import bpy,bmesh,math,json,hashlib,struct,sys
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/drowsy-glossy';OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_DrowsyGlossyEyes.blend'
BROWN_OPEN='--brown-open' in sys.argv
LABEL='Brown Glossy' if BROWN_OPEN else 'Drowsy Glossy'
PREFIX='Brown' if BROWN_OPEN else 'Drowsy'
if BROWN_OPEN:
    OUT=ROOT/'artifacts/smooth-bear-unity/expressions/brown-glossy';OUT.mkdir(parents=True,exist_ok=True)
    FILE=DEST/'BasicPlayerCapsule_Bear_BrownGlossyEyes.blend'
RAISED_NO_LOWER='--raised-no-lower' in sys.argv
if RAISED_NO_LOWER:
    OUT=ROOT/'artifacts/smooth-bear-unity/expressions/drowsy-raised-no-lower';OUT.mkdir(parents=True,exist_ok=True)
    FILE=DEST/'BasicPlayerCapsule_Bear_DrowsyGlossyEyes_NoLowerLids_Raised.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];rig.data.pose_position='REST';bpy.context.view_layer.update()
def fingerprint(obj):return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
keep={n:fingerprint(bpy.data.objects[n]) for n in ['Body','Hood','Shoes.L','Shoes.R','Mouth_Smile','Eye_White_L','Eye_White_R']}
collection=bpy.data.collections['EXPRESSION - Fierce Eyelids']
for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
collection.name='EXPRESSION - '+LABEL+' Eyes'
def linear(h):
    c=np.array([int(h[i:i+2],16)/255 for i in (1,3,5)])
    return np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
N=1536;axis=(np.arange(N,dtype=np.float32)+.5)/N*2-1;x,z=np.meshgrid(axis,axis)
px=x*.0948712;pz=z*.096954-(.002 if BROWN_OPEN else .004)
iris_x=.071 if BROWN_OPEN else .068;iris_z=.075 if BROWN_OPEN else .069
r=np.sqrt((px/iris_x)**2+(pz/iris_z)**2);angle=np.arctan2(pz/iris_z,px/iris_x)
pixels=np.ones((N,N,4),dtype=np.float32);pixels[:,:,:3]=linear('#FAFAF5')
def paint(alpha,c):
    a=np.clip(alpha,0,1)[...,None];pixels[:,:,:3]=pixels[:,:,:3]*(1-a)+c*a
iris_mask=np.clip((1-r)*N/8+.5,0,1)
bottom=np.clip(.45-.60*pz/.069,0,1)[...,None]
iris=linear('#3F2A23' if BROWN_OPEN else '#303947')*(1-bottom)+linear('#B9A08F' if BROWN_OPEN else '#A1ADBE')*bottom
# Fine radial fibres remain subtle at character viewing distance.
fibres=(np.sin(angle*63+np.sin(angle*13)*2+r*9)+.45*np.sin(angle*117-r*12))*.035
iris*=np.clip(1+fibres[...,None],.90,1.10)
paint(iris_mask,iris)
rim=np.clip((r-.87)/.07,0,1)*iris_mask
paint(rim,linear('#322421' if BROWN_OPEN else '#252C37'))
pupil=np.sqrt((px/(.042 if BROWN_OPEN else .035))**2+(pz/(.045 if BROWN_OPEN else .036))**2)
paint(np.clip((1-pupil)*N/8+.5,0,1)*iris_mask,linear('#12151D'))
# Highlights are part of the colour print, never separate floating geometry.
highlights=[(.029,.032,.010,.013),(.043,.015,.004,.005)] if BROWN_OPEN else [(.032,-.014,.008,.009),(-.030,-.041,.006,.009)]
for cx,cz,sx,sz in highlights:
    d=np.sqrt(((px-cx)/sx)**2+((pz-cz)/sz)**2)
    paint(np.clip((1-d)*N/12+.5,0,1)*iris_mask,linear('#FFFDF4'))
texture_name=PREFIX+'GlossyEyes_BaseColor'
image=bpy.data.images.new(texture_name,width=N,height=N,alpha=True)
image.pixels.foreach_set(pixels.ravel());image.update();image.filepath_raw=str(DEST/(texture_name+'.png'));image.file_format='PNG';image.save();image.pack()
skin=bpy.data.objects['Body'].data.materials[0].copy();skin.name='MAT_'+PREFIX+'_Eyelids_Skin'
created=[];specs={}
for side in ['L','R']:
    white=bpy.data.objects['Eye_White_'+side]
    points=[white.matrix_world@v.co for v in white.data.vertices]
    lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)))
    center=(lo+hi)/2;radius=(hi-lo)*.5*1.018;sign=1 if center.x>0 else -1
    mat=bpy.data.materials.new('MAT_'+PREFIX+'_Glossy_Print_'+side);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=image;uv=nodes.new('ShaderNodeUVMap');uv.uv_map='SparklePrintUV'
    links.new(uv.outputs['UV'],tex.inputs['Vector']);links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value=.24;shader.inputs['Coat Weight'].default_value=.16;shader.inputs['Coat Roughness'].default_value=.2
    white.data.materials.append(mat);front_index=len(white.data.materials)-1
    for poly in white.data.polygons:poly.material_index=front_index if sum(points[i].y for i in poly.vertices)/len(poly.vertices)<center.y else 0
    white['expression']=LABEL+' iris printed directly on sclera'
    lid_profiles=[('Upper',.004,True)] if RAISED_NO_LOWER else [('Upper',-.001,True),('Lower',-.061,False)]
    if BROWN_OPEN:lid_profiles=[('Upper',.064,True),('Lower',-.074,False)]
    for kind,offset,upper in lid_profiles:
        cut=center.z+offset
        slope=-sign*math.tan(math.radians(2 if BROWN_OPEN else 4)) if upper else 0
        curve=(-.054 if upper else .038) if BROWN_OPEN else (-.004 if upper else .018)
        mesh=bpy.data.meshes.new(f'{PREFIX}_{kind}Lid_{side}')
        mesh.from_pydata([center+(p-center)*1.018 for p in points],[],[tuple(p.vertices) for p in white.data.polygons]);mesh.update()
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
        def curve_offset(v):return curve*((v.x-center.x)/radius.x)**2
        for v in bm.verts:v.co.z-=curve_offset(v.co)
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(center.x,0,cut),plane_no=(-slope,0,1),clear_inner=upper,clear_outer=not upper)
        for v in bm.verts:v.co.z+=curve_offset(v.co)
        extreme=center.z+radius.z if upper else center.z-radius.z
        for v in bm.verts:
            d=v.co-center;edge=cut+slope*d.x+curve_offset(v.co)
            t=max(0,min(1,(v.co.z-edge)/(extreme-edge)))
            front=max(0,min(1,-d.y/radius.y));across=max(0,1-(d.x/radius.x)**2)
            v.co.y-=.0132*math.sin(math.pi*t)**.8*front*across
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
        assert len(mesh.polygons)>0
        obj=bpy.data.objects.new(f'Eye_{PREFIX}_{kind}Lid_'+side,mesh);collection.objects.link(obj);mesh.materials.append(skin)
        for p in mesh.polygons:p.use_smooth=True
        solid=obj.modifiers.new('Soft eyelid edge','SOLIDIFY');solid.thickness=.0013;solid.offset=-1
        bevel=obj.modifiers.new('Rounded eyelid rim','BEVEL');bevel.width=.0011;bevel.segments=3;bevel.limit_method='ANGLE'
        obj.vertex_groups.new(name='Head').add(list(range(len(mesh.vertices))),1,'REPLACE')
        arm=obj.modifiers.new('Follow Head','ARMATURE');arm.object=rig;obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted()
        obj['expression']=LABEL;created.append(obj)
    specs[side]={'eye_geometry_unchanged':True,'outer_corner_droop_degrees':4,'iris':'slate blue-grey radial gradient with dark pupil and rim','highlights':2,'padded_lids':True}
    if RAISED_NO_LOWER:specs[side].update({'lower_lid_present':False,'upper_lid_raise':.005})
    if BROWN_OPEN:specs[side].update({'outer_corner_droop_degrees':2,'iris':'warm brown gradient with large dark pupil and upper-right highlights','open_eye_center_height':.138,'curved_upper_and_lower_lids':True})
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
rig.data.pose_position='POSE';bpy.context.view_layer.update()
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24;cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render(PREFIX.lower()+'-glossy-front',(0,-5,1.88),(0,0,1.64),.99,1050,950)
if not RAISED_NO_LOWER:
    render(PREFIX.lower()+'-glossy-angle',(1.8,-5,1.9),(0,0,1.64),1.05,1000,900)
bpy.ops.object.select_all(action='DESELECT')
for obj in created:obj.select_set(True)
bpy.context.view_layer.objects.active=created[0]
readme=bpy.data.texts.new('DROWSY_GLOSSY_EYES_README')
readme.write('Drowsy Glossy Eyes\nSoft half-closed lids, outer corners drooping four degrees, curved lower lids.\nSlate blue-grey irises have a dark rim, pupil, fine radial fibres and two white reflections printed directly on the sclera.\nPacked texture: DrowsyGlossyEyes_BaseColor. No separate pupil or highlight geometry.\nEditable lids follow Head. Current eye size/spacing, body, hood, mouth and approved compact shoes are unchanged.\nThe approved fierce expression remains in its own file. This is a separate Blender draft.\n')
if RAISED_NO_LOWER:
    readme.write('\nRaised NoLowerLids revision: lower eyelids omitted, upper edge raised .005. Iris print, four-degree outer droop and other character geometry unchanged.\n')
if BROWN_OPEN:
    readme.name='BROWN_GLOSSY_EYES_README';readme.clear()
    readme.write('Brown Glossy Eyes\nWide, softly rounded eye opening framed by padded curved upper and lower lids.\nWarm brown iris, large dark pupil and upper-right reflections are printed on the original sclera.\nPacked texture: BrownGlossyEyes_BaseColor.\nEditable eyelids follow Head. Eye size/spacing, body, hood, smile and compact shoes are retained.\nThis is a separate Blender expression; approved expressions are preserved.\n')
for t in bpy.data.texts:t.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps({'file':str(FILE),'expression':LABEL,'eye_specs':specs,'unchanged_geometry':list(keep),'texture_packed':bool(image.packed_file)},indent=2),encoding='utf-8')
print('DROWSY_GLOSSY_VERIFIED '+str(FILE),flush=True)
