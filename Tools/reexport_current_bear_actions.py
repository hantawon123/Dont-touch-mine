"""Resume export from the completed all-actions Blender file."""
import bpy,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=Path(sys.argv[sys.argv.index('--')+1]);STAGED=OUT/'fbx'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'),use_scripts=False)
for image in bpy.data.images:
 if image.source=='FILE' and Path(image.filepath).name=='Color_Texture.png':
  image.filepath=str(ROOT/'Assets/DGN_15_CapsuleAnimals _V2/Textures/Color_Texture.png');image.reload();image.pack()
scene=bpy.context.scene;rig=bpy.data.objects['DGN_Armature'];body=bpy.data.objects['Body'];meshes=[o for o in scene.objects if o.type=='MESH']
assets=json.loads((OUT/'manifest.json').read_text());report=json.loads((OUT/'bake-report.json').read_text())
states=[s for a in assets for s in a['states']]
actions={a['clip']:bpy.data.actions[a['clip']+'_CurrentBear'] for a in assets}
shape_actions={a['clip']:bpy.data.actions[a['clip']+'_CurrentBear_Shapes'] for a in assets}
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'))
code=(ROOT/'Tools/retarget_current_bear_motions.py').read_text()
exec(code[code.index('# Bake subdivision/surface smoothing'):])
