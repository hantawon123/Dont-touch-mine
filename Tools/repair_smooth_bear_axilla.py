"""Export a replacement model with source shoulder weights and stable lower torso."""
import bpy,json,sys,shutil,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from smooth_bear_skinning import capture_weights,restore_upper_body
from hold_crawl_torso import FBX_KW
OUT=ROOT/'artifacts/smooth-bear-unity/axilla'
OUT.mkdir(exist_ok=True)
SOURCE=Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/supplied-bear-smooth/BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
source_weights=capture_weights(bpy.data.objects['Body'])
author=ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'
model=ROOT/'Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx'
for path in [author,model]:
    backup=OUT/('before-'+path.name)
    if not backup.exists():shutil.copy2(path,backup)
bpy.ops.wm.open_mainfile(filepath=str(author),use_scripts=False)
body=bpy.data.objects['Body'];rig=bpy.data.objects['DGN_Armature']
before=[tuple(v.co) for v in body.data.vertices]
changed=restore_upper_body(body,source_weights)
assert before==[tuple(v.co) for v in body.data.vertices]
assert len(body.data.shape_keys.key_blocks)==3
rig.animation_data_clear();rig.data.pose_position='POSE'
for bone in rig.pose.bones:bone.matrix_basis.identity()
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type=='MESH' or obj==rig:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
kwargs=FBX_KW.copy();kwargs.update(bake_anim=False,embed_textures=False,path_mode='AUTO')
bpy.ops.export_scene.fbx(filepath=str(OUT/'SmoothBear.fbx'),**kwargs)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SmoothBear.blend'),check_existing=False)
report={'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'vertices':len(before),'reweighted_vertices':changed,'base_geometry_unchanged':True,
        'blend_shapes':[k.name for k in body.data.shape_keys.key_blocks]}
(OUT/'repair-report.json').write_text(json.dumps(report,indent=2))
print('AXILLA_REPAIR_READY',report,flush=True)
