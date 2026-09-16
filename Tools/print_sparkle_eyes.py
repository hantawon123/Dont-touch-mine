"""Print the sparkle graphic on the existing sclera; fit liner to its face seam."""
import bpy,math,json,hashlib,struct
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/sparkle-printed';OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_SparkleEyes_Printed.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_SparkleEyes_Lashes.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];rig.data.pose_position='REST';bpy.context.view_layer.update()
def fingerprint(obj):return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
names=['Body','Hood','Shoes.L','Shoes.R','Eye_White_L','Eye_White_R']
keep={n:fingerprint(bpy.data.objects[n]) for n in names}
collection=bpy.data.collections['EXPRESSION - Sparkle Eyes']
for obj in list(collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
collection.name='EXPRESSION - Sparkle Printed Liner'
def bvh(obj):
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
    tree=BVHTree.FromPolygons([evaluated.matrix_world@v.co for v in mesh.vertices],[tuple(p.vertices) for p in mesh.polygons])
    evaluated.to_mesh_clear();return tree
face=bvh(bpy.data.objects['Body'])
def hit(tree,x,z):
    loc,normal,index,distance=tree.ray_cast(Vector((x,-2,z)),Vector((0,1,0)),4)
    return loc
def linear(h):
    values=np.array([int(h[i:i+2],16)/255 for i in (1,3,5)])
    return np.where(values<=.04045,values/12.92,((values+.055)/1.055)**2.4)
# The graphic is a Base Color image on the sclera UVs, with no raised pupil geometry.
resolution=1536
axis=(np.arange(resolution,dtype=np.float32)+.5)/resolution*2-1
x,z=np.meshgrid(axis,axis)
pixels=np.ones((resolution,resolution,4),dtype=np.float32);pixels[:,:,:3]=linear('#FAFAF5')
def paint(mask,h):
    a=np.clip(mask,0,1)[...,None];pixels[:,:,:3]=pixels[:,:,:3]*(1-a)+linear(h)*a
# Normalized to the current white eyeball's front-projected dimensions.
rx=.0937462;rz=.0958043
ox=x*rx;oz=z*rz-.002
paint((1-np.sqrt((ox/.074)**2+(oz/.080)**2))*resolution/3+.5,'#12121C')
for cx,cz,sx,sz in [(-.015,.014,.033,.043),(.031,-.030,.015,.020)]:
    d=np.abs((ox-cx)/sx)**(2/3)+np.abs((oz-cz)/sz)**(2/3)
    paint((1-d)*resolution/10+.5,'#FFF3A1')
for cx,cz,sx,sz in [(.035,.048,.010,.013),(-.041,-.044,.0045,.006)]:
    d=np.sqrt(((ox-cx)/sx)**2+((oz-cz)/sz)**2)
    paint((1-d)*resolution/8+.5,'#FFFDF2')
image=bpy.data.images.new('SparkleEyes_Print_BaseColor',width=resolution,height=resolution,alpha=True)
image.pixels.foreach_set(pixels.ravel());image.update()
image.filepath_raw=str(DEST/'SparkleEyes_Print_BaseColor.png');image.file_format='PNG';image.save();image.pack()
created=[];measurements={}
liner_mat=bpy.data.materials['MAT_Eyes_Sparkle_Liner']
def tube(name,coords,radii,width):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=12
    curve.bevel_depth=width;curve.bevel_resolution=4;curve.use_fill_caps=True
    spline=curve.splines.new('BEZIER');spline.bezier_points.add(len(coords)-1)
    for point,co,r in zip(spline.bezier_points,coords,radii):
        point.co=co;point.radius=r;point.handle_left_type='AUTO';point.handle_right_type='AUTO'
    obj=bpy.data.objects.new(name,curve);collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj;bpy.ops.object.convert(target='MESH')
    obj=bpy.context.object;obj.data.materials.append(liner_mat)
    for p in obj.data.polygons:p.use_smooth=True
    obj.vertex_groups.new(name='Head').add(list(range(len(obj.data.vertices))),1,'REPLACE')
    mod=obj.modifiers.new('Follow Head','ARMATURE');mod.object=rig;obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted()
    obj['expression']='Sparkle Printed';created.append(obj)
for side in ['L','R']:
    white=bpy.data.objects['Eye_White_'+side]
    points=[white.matrix_world@v.co for v in white.data.vertices]
    lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)))
    center=(lo+hi)/2;radius=(hi-lo)/2;sign=1 if center.x>0 else -1
    layer=white.data.uv_layers.get('SparklePrintUV') or white.data.uv_layers.new(name='SparklePrintUV')
    for poly in white.data.polygons:
        for loop in poly.loop_indices:
            p=points[white.data.loops[loop].vertex_index]
            layer.data[loop].uv=((p.x-lo.x)/(hi.x-lo.x),(p.z-lo.z)/(hi.z-lo.z))
    mat=bpy.data.materials.new('MAT_Eye_Sparkle_Print_'+side);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;shader=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear'
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map=layer.name;links.new(uv.outputs['UV'],tex.inputs['Vector'])
    links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Roughness'].default_value=.34;shader.inputs['Metallic'].default_value=0
    # A single continuous eye surface: the printed regions have no displacement or bump.
    white.data.materials.append(mat);front_index=len(white.data.materials)-1
    for poly in white.data.polygons:
        y=sum(points[i].y for i in poly.vertices)/len(poly.vertices)
        poly.material_index=front_index if y<center.y else 0
    white['expression']='Sparkle printed directly in Base Color';eye=bvh(white)
    seam_samples=[]
    def seam(theta):
        dx=radius.x*math.cos(theta);dz=radius.z*math.sin(theta)
        def inside(r):
            p=hit(face,center.x+dx*r,center.z+dz*r)
            if p is None:return False
            nearest,normal,index,distance=eye.find_nearest(p)
            return (p-nearest).dot(normal)<=0
        low,high=0.,1.15
        assert inside(low),'Face does not intersect the white eyeball'
        for _ in range(26):
            mid=(low+high)/2
            if inside(mid):low=mid
            else:high=mid
        px,pz=center.x+dx*low,center.z+dz*low
        facep=hit(face,px,pz)
        assert facep is not None
        nearest,normal,index,distance=eye.find_nearest(facep)
        seam_samples.append(distance)
        return Vector((px,facep.y-.0007,pz))
    # Shift the angular extent outward while staying on the true contact contour.
    angles=[math.radians(22+136*i/40-sign*8) for i in range(41)]
    tube('Eye_Sparkle_UpperLid_'+side,[seam(a) for a in angles],[.6]+[1]*39+[.6],.003)
    for index,angle in enumerate([32,53],1):
        theta=math.radians((angle if sign>0 else 180-angle)-sign*8)
        anchor=seam(theta);length=.027 if index==1 else .030
        coords=[anchor]
        for dx,dz in [(sign*length*.58,length*.20),(sign*length*.78,length*.86)]:
            px,pz=anchor.x+dx,anchor.z+dz;p=hit(face,px,pz)
            coords.append(Vector((px,p.y-.0016 if p is not None else anchor.y,pz)))
        tube(f'Eye_Sparkle_Lash{index}_{side}',coords,[1,.85,.13],.0032)
    measurements[side]={'contact_samples':len(seam_samples),'max_contact_depth_error':max(seam_samples,default=0),'lashes':2}
    assert max(seam_samples,default=0)<.002,measurements[side]
rig.data.pose_position='POSE';bpy.context.view_layer.update()
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24;cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('sparkle-printed-front',(0,-5,1.88),(0,0,1.64),.99,1050,950)
render('sparkle-printed-angle',(2,-5,1.9),(0,0,1.64),1.05,1000,900)
readme=bpy.data.texts.get('SPARKLE_EYES_README');readme.clear()
readme.write('Sparkle Printed Eyes\nEye_White_L/R use the packed SparkleEyes_Print_BaseColor texture with SparklePrintUV.\nPupil, stars and glints are colour printed directly on the original sclera surface, with no separate spheres, bump or displacement.\nUpper lids follow the measured face/sclera intersection. Two rooted lashes per eye.\nBody, hood, compact shoes, eye geometry, size and spacing are unchanged.\nBlender-only expression draft.\n')
for t in bpy.data.texts:t.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
report={'file':str(FILE),'texture':str(DEST/'SparkleEyes_Print_BaseColor.png'),'pupil_and_sparkles':'Base Color texture on original sclera; no separate geometry','unchanged_geometry':names,'liner_contact':measurements}
(OUT/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PRINTED_SPARKLE_VERIFIED '+str(FILE),flush=True)
