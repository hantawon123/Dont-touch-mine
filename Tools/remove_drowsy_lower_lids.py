"""Remove only the lower eyelid objects from the latest drowsy expression."""
import bpy,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Expressions'
OUT=ROOT/'artifacts/smooth-bear-unity/expressions/drowsy-no-lower-lids';OUT.mkdir(parents=True,exist_ok=True)
FILE=DEST/'BasicPlayerCapsule_Bear_DrowsyGlossyEyes_NoLowerLids.blend'
bpy.ops.wm.open_mainfile(filepath=str(DEST/'BasicPlayerCapsule_Bear_DrowsyGlossyEyes.blend'),use_scripts=False)
def fingerprint(obj):return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in obj.data.vertices)).hexdigest()
removed=['Eye_Drowsy_LowerLid_L','Eye_Drowsy_LowerLid_R']
keep={o.name:fingerprint(o) for o in bpy.data.objects if o.type=='MESH' and o.name not in removed}
for name in removed:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
assert all(name not in bpy.data.objects for name in removed)
assert all(fingerprint(bpy.data.objects[n])==v for n,v in keep.items())
scene=bpy.context.scene;scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24
cam=scene.camera;cam.location=(0,-5,1.88);cam.rotation_euler=(Vector((0,0,1.64))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.99
scene.render.resolution_x=1050;scene.render.resolution_y=950;scene.render.filepath=str(OUT/'drowsy-no-lower-lids-front.png')
bpy.ops.render.render(write_still=True)
readme=bpy.data.texts.get('DROWSY_GLOSSY_EYES_README')
readme.write('\nNoLowerLids revision: both lower eyelid objects removed. Sclera, printed iris, upper lids and all other geometry unchanged.\n')
for t in bpy.data.texts:t.use_module=False
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps({'file':str(FILE),'removed_objects':removed,'all_other_mesh_geometry_unchanged':True},indent=2),encoding='utf-8')
print('LOWER_LIDS_REMOVED_VERIFIED '+str(FILE),flush=True)
