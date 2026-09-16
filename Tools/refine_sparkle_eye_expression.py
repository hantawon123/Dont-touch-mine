"""Smaller spaced sparkle eyes, outward tilted liner and two lashes per eye."""
import bpy,math,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/sparkle-refined';OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_SparkleEyes_Lashes.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_SparkleEyes.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature']
rig.data.pose_position='REST';bpy.context.view_layer.update()
def fingerprint(obj):
    return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
keep={name:fingerprint(bpy.data.objects[name]) for name in ['Body','Hood','Shoes.L','Shoes.R']}
collection=bpy.data.collections['EXPRESSION - Sparkle Eyes']
size=.92;spacing=.010;tilt=math.radians(8)
report={'file':str(FILE),'eye_scale':size,'outward_offset_per_eye':spacing,'liner_outward_tilt_degrees':8,'lashes':{}}
for side in ['L','R']:
    white=bpy.data.objects['Eye_White_'+side]
    center=white.matrix_world.translation.copy();sign=1 if center.x>0 else -1
    shift=Vector((sign*spacing,0,0))
    original_center=center.copy()
    # Shared transform keeps the whites, pupils, stars and glints aligned.
    elements=[white,bpy.data.objects['Eye_Pupil_'+side]]+[o for o in collection.objects if o.name.endswith('_'+side)]
    for obj in elements:
        matrix=obj.matrix_world.copy();inverse=matrix.inverted()
        for vertex in obj.data.vertices:
            vertex.co=inverse@(center+(matrix@vertex.co-center)*size+shift)
        obj.data.update()
    center+=shift
    liner=bpy.data.objects['Eye_Sparkle_UpperLid_'+side]
    pivot=Vector((center.x,center.y,center.z+.002*size))
    def rotate(point):
        p=point-pivot;c=math.cos(tilt);s=sign*math.sin(tilt)
        return pivot+Vector((c*p.x+s*p.z,p.y,-s*p.x+c*p.z))
    matrix=liner.matrix_world.copy();inverse=matrix.inverted()
    for vertex in liner.data.vertices:vertex.co=inverse@rotate(matrix@vertex.co)
    liner.data.update()
    report['lashes'][side]=[]
    for index,angle in enumerate([32,53],1):
        t=math.radians(angle)
        anchor=Vector((center.x+sign*.096*size*math.cos(t),center.y-.026*size,pivot.z+.098*size*math.sin(t)))
        # Two separated, tapered strands curl outward and upward from the liner.
        length=.027 if index==1 else .030
        pts=[anchor,anchor+Vector((sign*length*.58,-.001,length*.20)),anchor+Vector((sign*length*.78,-.0015,length*.86))]
        pts=[rotate(p) for p in pts]
        name=f'Eye_Sparkle_Lash{index}_{side}'
        curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=16
        curve.bevel_depth=.0035;curve.bevel_resolution=4;curve.use_fill_caps=True
        spline=curve.splines.new('BEZIER');spline.bezier_points.add(2)
        for point,coord,radius in zip(spline.bezier_points,pts,[1,.85,.13]):
            point.co=coord;point.radius=radius;point.handle_left_type='AUTO';point.handle_right_type='AUTO'
        obj=bpy.data.objects.new(name,curve);collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target='MESH');obj=bpy.context.object
        obj.data.materials.append(bpy.data.materials['MAT_Eyes_Sparkle_Liner'])
        for poly in obj.data.polygons:poly.use_smooth=True
        obj.vertex_groups.new(name='Head').add(list(range(len(obj.data.vertices))),1,'REPLACE')
        mod=obj.modifiers.new('Follow Head','ARMATURE');mod.object=rig
        obj.parent=rig;obj.matrix_parent_inverse=rig.matrix_world.inverted()
        obj['expression']='Sparkle';report['lashes'][side].append(obj.name)
    assert len(report['lashes'][side])==2
rig.data.pose_position='POSE';bpy.context.view_layer.update()
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
report['body_hood_approved_shoes_unchanged']=True
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24
cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('sparkle-lashes-closeup',(0,-5,1.88),(0,0,1.64),.99,1050,950)
render('sparkle-lashes-three-quarter',(1.1,-5,1.9),(0,0,1.64),1.05,1000,900)
bpy.ops.object.select_all(action='DESELECT')
for obj in collection.objects:obj.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Eye_Sparkle_Pupil_L']
readme=bpy.data.texts.get('SPARKLE_EYES_README')
readme.write('\nRefinement: all eye components scaled to 92%, shifted outward by 0.01 per side.\nUpper liner tilted outward 8 degrees; two separate tapered lashes on each eye.\n')
for text in bpy.data.texts:text.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SPARKLE_LASHES_VERIFIED '+str(FILE),flush=True)
