"""Finish editable shoe UVs, character display materials, and delivery views."""
import bpy,bmesh,math,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/smooth-bear-unity/shoes'
FILE=ROOT/'source/blender/characters/SmoothBear/Shoes/BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes.blend'
def fingerprint(mesh):
    return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in mesh.vertices)).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/SmoothBear/SmoothBear.blend'),use_scripts=False)
original=fingerprint(bpy.data.objects['Body'].data)
bpy.ops.wm.open_mainfile(filepath=str(FILE),use_scripts=False)
assert fingerprint(bpy.data.objects['Body'].data)==original
scene=bpy.context.scene;shoes=[bpy.data.objects['Shoes.'+side] for side in ['L','R']]
def color(hex):
    vals=[int(hex[i:i+2],16)/255 for i in (1,3,5)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in vals)+(1,)
# Match the current Unity character's authored colours in this separate file.
for name,hex in [('Body','#F3D646'),('Hood','#D4ECFF'),('Eye_Pupil_L','#20202B'),('Eye_Pupil_R','#20202B'),
                 ('Eye_White_L','#FAFAF5'),('Eye_White_R','#FAFAF5'),('Mouth_Smile','#9C536D')]:
    obj=bpy.data.objects[name]
    mat=obj.data.materials[0].copy() if len(obj.data.materials) and obj.data.materials[0] else bpy.data.materials.new('Preview_'+name)
    mat.name='Preview_'+name;mat.use_nodes=True
    if len(obj.data.materials):obj.data.materials[0]=mat
    else:obj.data.materials.append(mat)
    mat.diffuse_color=color(hex)
    node=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if mat.node_tree else None
    if node:
        for link in list(node.inputs['Base Color'].links):mat.node_tree.links.remove(link)
        node.inputs['Base Color'].default_value=color(hex);node.inputs['Metallic'].default_value=0
        node.inputs['Roughness'].default_value=.58
        if 'Emission Strength' in node.inputs:node.inputs['Emission Strength'].default_value=0
        if 'Subsurface Weight' in node.inputs:node.inputs['Subsurface Weight'].default_value=0
bpy.ops.object.select_all(action='DESELECT')
for obj in shoes:obj.hide_set(False);obj.select_set(True)
bpy.context.view_layer.objects.active=shoes[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
bpy.ops.object.mode_set(mode='OBJECT')
report=json.loads((OUT/'report.json').read_text());report['body_geometry_unchanged']=True
report['mesh_validation']={}
for obj in shoes:
    bm=bmesh.new();bm.from_mesh(obj.data)
    assert all(edge.is_manifold for edge in bm.edges)
    assert bm.calc_volume()>0
    report['mesh_validation'][obj.name]={'manifold':True,'uv_layers':len(obj.data.uv_layers),'volume':bm.calc_volume()}
    bm.free()
scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.4,.45,.55,1);bg.inputs['Strength'].default_value=.35
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.view_settings.exposure=-1.25
scene.render.threads_mode='FIXED';scene.render.threads=8;scene.cycles.samples=24
cam=scene.camera;cam.data.clip_end=500
def render(name,pos,target,scale,w,h):
    cam.location=pos;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('shoes-character-front',(0,-5,1.3),(0,0,1.1),2.85,900,1100)
render('shoes-detail',(1.3,-2.7,1.0),(0,-.055,.12),.96,1100,850)
# Save an uncluttered, front-facing material preview with both shoes selected.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL';space.overlay.show_floor=False
            space.overlay.show_axis_x=False;space.overlay.show_axis_y=False
            space.region_3d.view_location=(0,0,1.0);space.region_3d.view_distance=3.8
scene['shoe_draft_note']='Blender draft only. Separate Shoes.L/R, editable subdivision, UVs and independent shoe material. Body vertices unchanged.'
report['removed_missing_legacy_images']=[]
for image in list(bpy.data.images):
    if image.source=='FILE' and not image.packed_file and not Path(bpy.path.abspath(image.filepath)).is_file():
        report['removed_missing_legacy_images'].append(image.name)
        bpy.data.images.remove(image)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(FILE))
(OUT/'report.json').write_text(json.dumps(report,indent=2))
print('SHOE_DRAFT_VERIFIED '+str(FILE),flush=True)
