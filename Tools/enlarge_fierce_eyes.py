"""Enlarge the complete latest fierce eyes around their current centers."""
import bpy,hashlib,struct,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/fierce-bigger';OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_FierceEyes_Bigger.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_FierceEyes_LargeIris_RaisedMore.blend'),use_scripts=False)
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];rig.data.pose_position='REST';bpy.context.view_layer.update()
def fingerprint(obj):return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
keep={n:fingerprint(bpy.data.objects[n]) for n in ['Body','Hood','Shoes.L','Shoes.R','Mouth_Smile']}
scale=1.10;report={'file':str(FILE),'scale':scale,'eyes':{}};selected=[]
for side in ['L','R']:
    white=bpy.data.objects['Eye_White_'+side]
    points=[white.matrix_world@v.co for v in white.data.vertices]
    lo=Vector(tuple(min(p[k] for p in points) for k in range(3)));hi=Vector(tuple(max(p[k] for p in points) for k in range(3)))
    center=(lo+hi)/2
    elements=[white,bpy.data.objects['Eye_Fierce_UpperLid_'+side],bpy.data.objects['Eye_Fierce_LowerLid_'+side]]
    for obj in elements:
        matrix=obj.matrix_world.copy();inverse=matrix.inverted()
        for vertex in obj.data.vertices:
            vertex.co=inverse@(center+(matrix@vertex.co-center)*scale)
        obj.data.update()
        for mod in obj.modifiers:
            if mod.type=='SOLIDIFY':mod.thickness*=scale
            elif mod.type=='BEVEL':mod.width*=scale
        selected.append(obj)
    after=[white.matrix_world@v.co for v in white.data.vertices]
    new_lo=Vector(tuple(min(p[k] for p in after) for k in range(3)));new_hi=Vector(tuple(max(p[k] for p in after) for k in range(3)))
    assert ((new_lo+new_hi)/2-center).length<.000001
    assert all(abs((new_hi[k]-new_lo[k])/(hi[k]-lo[k])-scale)<.00002 for k in range(3))
    report['eyes'][side]={'center_unchanged':True,'center':list(center),'old_dimensions':list(hi-lo),'new_dimensions':list(new_hi-new_lo),'iris_and_glint':'UV print scaled with the sclera','lid_slope_degrees':9}
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
report['body_hood_mouth_shoes_unchanged']=True
rig.data.pose_position='POSE';bpy.context.view_layer.update()
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24;cam=scene.camera
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
render('fierce-bigger-front',(0,-5,1.88),(0,0,1.64),.99,1050,950)
render('fierce-bigger-angle',(1.8,-5,1.9),(0,0,1.64),1.05,1000,900)
bpy.ops.object.select_all(action='DESELECT')
for obj in selected:obj.select_set(True)
bpy.context.view_layer.objects.active=selected[0]
readme=bpy.data.texts.get('FIERCE_EYES_README');readme.write('\nBigger eyes: complete sclera, printed iris/glint and upper/lower eyelids scaled to 110% around each existing eye center. Center spacing and slope remain unchanged.\n')
for t in bpy.data.texts:t.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BIGGER_EYES_VERIFIED '+str(FILE),flush=True)
