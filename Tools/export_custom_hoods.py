"""Extract supplied animal hoods into the current character's hood coordinates."""
import bpy,sys,json,hashlib
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from hold_crawl_torso import FBX_KW
OUT=ROOT/'artifacts/smooth-bear-unity/customization';OUT.mkdir(parents=True,exist_ok=True)
source_dir=ROOT/'source/blender/characters/SmoothBear/Hoods'
parts={};report={}
for animal in ('Cat','Dog','Rabbit'):
    source=source_dir/f'BasicPlayerCapsule_WithWalk_{animal}_Ears.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source),use_scripts=False)
    hood=bpy.data.objects['Hood'];rig=bpy.data.objects['DGN_Armature']
    rig.animation_data_clear();rig.data.pose_position='REST';bpy.context.view_layer.update()
    evaluated=hood.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
    temp=bpy.data.objects.new('ExtractedHood',mesh);bpy.context.scene.collection.objects.link(temp)
    temp.matrix_world=hood.matrix_world
    # Save an intermediate library, retaining UVs and the evaluated solid shell.
    bpy.data.libraries.write(str(OUT/f'{animal}.blend'),{temp},fake_user=True)
    report[animal]={'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'uv_layers':len(mesh.uv_layers),'matrix_world':[list(r) for r in hood.matrix_world]}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'),use_scripts=False)
rig=bpy.data.objects['DGN_Armature'];rig.animation_data_clear();rig.data.pose_position='REST'
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.update();authored=bpy.data.objects['Hood'];basis=authored.matrix_world.copy();export=[]
for animal in ('Cat','Dog','Rabbit'):
    with bpy.data.libraries.load(str(OUT/f'{animal}.blend'),link=False) as (src,dst):dst.objects=['ExtractedHood']
    obj=dst.objects[0];bpy.context.scene.collection.objects.link(obj)
    obj.data.transform(basis.inverted()@Matrix(report[animal]['matrix_world']))
    obj.name='Hood_'+animal;obj.parent=authored.parent;obj.parent_type=authored.parent_type;obj.parent_bone=authored.parent_bone;obj.matrix_parent_inverse=authored.matrix_parent_inverse.copy();obj.matrix_basis=authored.matrix_basis.copy()
    obj.data.materials.clear();obj.data.materials.append(authored.data.materials[0])
    for p in obj.data.polygons:p.material_index=0;p.use_smooth=True
    export.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in export+[rig]:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
kwargs=FBX_KW.copy();kwargs.update(bake_anim=False,embed_textures=False,path_mode='AUTO')
bpy.ops.export_scene.fbx(filepath=str(OUT/'AnimalHoods.fbx'),**kwargs)
(OUT/'extraction-report.json').write_text(json.dumps(report,indent=2))
print('EXPORTED_ANIMAL_HOODS',report,flush=True)
