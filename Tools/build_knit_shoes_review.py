"""Apply the approved straight hood knit to the compact shoes for Blender review."""
import bpy, json, hashlib, struct, math
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'source/blender/characters/SmoothBear/Hoods/StraightKnit_Review/BasicPlayerCapsule_Bear_StraightKnit.blend'
DEST=ROOT/'source/blender/characters/SmoothBear/Shoes/BasicPlayerCapsule_Bear_KnitShoes_Review.blend'
OUT=ROOT/'artifacts/smooth-bear-unity/knit-shoes-review'
OUT.mkdir(parents=True,exist_ok=True)
source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
for t in bpy.data.texts:t.use_module=False
def fingerprint(mesh):
    return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in mesh.vertices)).hexdigest()
original_geometry={o.name:fingerprint(o.data) for o in bpy.data.objects if o.type=='MESH'}
scene=bpy.context.scene
shoes=[bpy.data.objects['Shoes.'+side] for side in ['L','R']]
hood=bpy.data.objects['Hood'];fabric=hood.data.materials[0]
original_slots={o.name:list(o.data.materials) for o in shoes}
prefs=bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type='OPTIX';prefs.get_devices()
    for d in prefs.devices:d.use=d.type=='OPTIX'
    scene.cycles.device='GPU' if any(d.type=='OPTIX' for d in prefs.devices) else 'CPU'
except Exception:scene.cycles.device='CPU'
scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.threads_mode='FIXED';scene.render.threads=8
def render(name,pos,target,scale,w,h):
    cam=scene.camera;cam.location=pos
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('shoes-before',(1.25,-2.8,1.15),(0,-.02,.15),.95,1100,850)
copies={}
for obj in shoes:
    # Match the hood's physical rib spacing; preserve all authored UVs and mesh data.
    for name,axis in [('Knit_Front_Straight',0),('Knit_Side_Straight',1)]:
        layer=obj.data.uv_layers.get(name) or obj.data.uv_layers.new(name=name)
        for loop in obj.data.loops:
            p=obj.data.vertices[loop.vertex_index].co
            layer.data[loop.index].uv=(p[axis]*obj.scale[axis]/hood.scale[axis],p.z*obj.scale.z/hood.scale.z)
    for slot,old in enumerate(original_slots[obj.name]):
        if old.name not in copies:
            old.use_fake_user=True
            mat=fabric.copy();mat.name=old.name+'_StraightKnit_REVIEW'
            old_bs=next(n for n in old.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
            bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
            bs.inputs['Base Color'].default_value=old_bs.inputs['Base Color'].default_value
            mat.diffuse_color=bs.inputs['Base Color'].default_value
            mat['source_fabric']=fabric.name
            copies[old.name]=mat
        obj.data.materials[slot]=copies[old.name]
render('shoes-after',(1.25,-2.8,1.15),(0,-.02,.15),.95,1100,850)
render('character-after',(0,-5,1.4),(0,0,1.10),2.65,900,1100)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.shading.type='MATERIAL'
            space.region_3d.view_location=(0,0,1.0);space.region_3d.view_distance=3.6
            space.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)
bpy.ops.object.select_all(action='DESELECT')
for obj in shoes:obj.select_set(True)
bpy.context.view_layer.objects.active=shoes[0]
note=bpy.data.texts.new('KNIT_SHOES_README')
note.write('Blender review only. Compact approved shoe geometry unchanged. Shoe upper and sole use copies of the straight hood knit material, retaining their original base colours. Hood rib spacing, bump relief, roughness and sheen are shared. Original shoe materials retained for comparison. No Unity changes.\n')
assert all(fingerprint(bpy.data.objects[n].data)==h for n,h in original_geometry.items())
bpy.ops.file.pack_all();scene.render.filepath='//KnitShoes_preview.png'
bpy.ops.wm.save_as_mainfile(filepath=str(DEST))
bpy.ops.wm.open_mainfile(filepath=str(DEST),use_scripts=False)
assert all(fingerprint(bpy.data.objects[n].data)==h for n,h in original_geometry.items())
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==source_hash
for obj in [bpy.data.objects['Shoes.L'],bpy.data.objects['Shoes.R']]:
    assert all(n in obj.data.uv_layers for n in ['Knit_Front_Straight','Knit_Side_Straight'])
    assert all('StraightKnit_REVIEW' in m.name for m in obj.data.materials)
(OUT/'verification.json').write_text(json.dumps({'file':str(DEST),'source_unchanged':True,'all_mesh_geometry_unchanged':True,'saved_file_reopened':True,'shoe_materials':[m.name for m in bpy.data.objects['Shoes.L'].data.materials]},indent=2))
print('KNIT_SHOES_VERIFIED '+str(DEST),flush=True)
