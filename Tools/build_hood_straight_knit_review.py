"""Blender-only review: continuous straight knit, without changing hood geometry."""
import bpy, math, json, hashlib, struct, sys
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'source/blender/characters/SmoothBear/Hoods/StraightKnit_Review'
OUT=ROOT/'artifacts/smooth-bear-unity/hood-knit-review'
DEST.mkdir(parents=True,exist_ok=True); OUT.mkdir(parents=True,exist_ok=True)
ANIMALS=['Bear','Cat','Dog','Rabbit']
if '--only' in sys.argv:ANIMALS=[sys.argv[sys.argv.index('--only')+1]]
BASE=ROOT/'source/blender/characters/SmoothBear/Expressions/BasicPlayerCapsule_Bear_BrownGlossyEyes.blend'

def fingerprint(mesh):
    return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in mesh.vertices)).hexdigest()

def rest():
    rig=bpy.data.objects.get('DGN_Armature')
    if rig:
        rig.animation_data_clear();rig.data.pose_position='REST'
        for b in rig.pose.bones:b.matrix_basis.identity()
    bpy.context.view_layer.update()

def linear(h):
    c=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    return tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in c)+(1,)

def add_uv(mesh):
    # Same metre-based origin for the shell and every ear: no per-island rescaling,
    # polar singularity, or angular coordinate. Keep the authored UV layer as backup.
    for name,axis in [('Knit_Front_Straight',0),('Knit_Side_Straight',1)]:
        layer=mesh.uv_layers.get(name) or mesh.uv_layers.new(name=name)
        for loop in mesh.loops:
            p=mesh.vertices[loop.vertex_index].co
            layer.data[loop.index].uv=(p[axis],p.z)
    mesh.uv_layers.active_index=mesh.uv_layers.find('Knit_Front_Straight')

def knit_material():
    mat=bpy.data.materials.new('MAT_Hood_StraightKnit_REVIEW');mat.use_nodes=True
    mat.diffuse_color=linear('D4ECFF')
    n=mat.node_tree.nodes;l=mat.node_tree.links;n.clear()
    def node(kind,name,xy):
        a=n.new(kind);a.name=name;a.label=name;a.location=xy;return a
    def mathnode(op,name,a,b=None,xy=(0,0)):
        m=node('ShaderNodeMath',name,xy);m.operation=op
        for i,v in enumerate([a,b]):
            if v is not None:
                if isinstance(v,(float,int)):m.inputs[i].default_value=v
                else:l.new(v,m.inputs[i])
        return m.outputs[0]
    bs=node('ShaderNodeBsdfPrincipled','Soft knitted fabric',(850,160))
    bs.inputs['Base Color'].default_value=mat.diffuse_color
    bs.inputs['Roughness'].default_value=.88;bs.inputs['Sheen Weight'].default_value=.18
    output=node('ShaderNodeOutputMaterial','Output',(1110,160));l.new(bs.outputs['BSDF'],output.inputs['Surface'])
    bumps=[]
    for j,uvname in enumerate(['Knit_Front_Straight','Knit_Side_Straight']):
        y=400-j*350
        uv=node('ShaderNodeUVMap',uvname,(-1100,y));uv.uv_map=uvname
        sep=node('ShaderNodeSeparateXYZ','Straight coordinate '+str(j),(-880,y));l.new(uv.outputs['UV'],sep.inputs[0])
        freq=mathnode('MULTIPLY','Rib spacing 22mm '+str(j),sep.outputs['X'],math.tau/.022,(-680,y))
        wave=mathnode('SINE','Parallel ribs '+str(j),freq,xy=(-490,y))
        bump=node('ShaderNodeBump','Soft rib relief '+str(j),(-230,y))
        bump.inputs['Strength'].default_value=.42;bump.inputs['Distance'].default_value=.001
        l.new(wave,bump.inputs['Height']);bumps.append(bump)
    # Blend the two vertical projections only around the side walls. This avoids
    # stretched side texture without visible box-projection seams or radial ears.
    geo=node('ShaderNodeNewGeometry','Surface normal',(-1100,-420))
    transform=node('ShaderNodeVectorTransform','Normal to hood space',(-890,-420))
    transform.vector_type='NORMAL';transform.convert_from='WORLD';transform.convert_to='OBJECT'
    l.new(geo.outputs['Normal'],transform.inputs[0])
    sep=node('ShaderNodeSeparateXYZ','Side orientation',(-670,-420));l.new(transform.outputs[0],sep.inputs[0])
    x=mathnode('ABSOLUTE','Abs side',sep.outputs['X'],xy=(-490,-420))
    y=mathnode('ABSOLUTE','Abs front',sep.outputs['Y'],xy=(-490,-580))
    x=mathnode('POWER','Side weight',x,8,(-300,-420));y=mathnode('POWER','Front weight',y,8,(-300,-580))
    denom=mathnode('ADD','Weight sum',x,y,(-100,-420))
    denom=mathnode('MAXIMUM','Avoid zero',denom,.000001,(70,-420))
    w=mathnode('DIVIDE','Smooth side blend',x,denom,(230,-420))
    mix=node('ShaderNodeMixRGB','Blend fabric normals',(240,200));mix.blend_type='MIX'
    l.new(w,mix.inputs[0]);l.new(bumps[0].outputs['Normal'],mix.inputs[1]);l.new(bumps[1].outputs['Normal'],mix.inputs[2])
    norm=node('ShaderNodeVectorMath','Unit normal',(430,200));norm.operation='NORMALIZE';l.new(mix.outputs[0],norm.inputs[0])
    tex=node('ShaderNodeTexCoord','Fine fibres',(-180,-740))
    noise=node('ShaderNodeTexNoise','Subtle yarn grain',(60,-740));noise.inputs['Scale'].default_value=330;noise.inputs['Detail'].default_value=2
    l.new(tex.outputs['Object'],noise.inputs['Vector'])
    grain=node('ShaderNodeBump','Fine fibres relief',(630,160));grain.inputs['Strength'].default_value=.075;grain.inputs['Distance'].default_value=.00045
    l.new(noise.outputs['Fac'],grain.inputs['Height']);l.new(norm.outputs[0],grain.inputs['Normal']);l.new(grain.outputs['Normal'],bs.inputs['Normal'])
    mat['rib_spacing_local_units']=.022;mat['review_only']=True
    return mat

def track(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
def studio(scene):
    for o in list(scene.objects):
        if o.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(o,do_unlink=True)
        elif o.name.startswith(('Preview_Ground','Preview_Backdrop')):o.hide_render=True
    cam=bpy.data.objects.new('KnitReview_Camera',bpy.data.cameras.new('KnitReview_Camera'));scene.collection.objects.link(cam)
    cam.data.type='ORTHO';scene.camera=cam
    for name,pos,energy,size in [('Key',(-3,-4,5),480,3),('Fill',(3,-2,3),180,3),('Rim',(0,3,4),260,3)]:
        o=bpy.data.objects.new('KnitReview_'+name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(o)
        o.location=pos;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;track(o,(0,0,1.8))
    scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.19,.22,.27,1)
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.45
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    prefs=bpy.context.preferences.addons['cycles'].preferences
    try:
        prefs.compute_device_type='OPTIX';prefs.get_devices()
        devices=[d for d in prefs.devices if d.type=='OPTIX']
        for device in prefs.devices:device.use=device.type=='OPTIX'
        if devices:scene.cycles.device='GPU'
        print('RENDER_DEVICES '+str([(d.name,d.type,d.use) for d in prefs.devices]),flush=True)
    except Exception as exc:print('GPU fallback: '+str(exc),flush=True)
    scene.render.threads_mode='FIXED';scene.render.threads=8
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
    return cam

def render(scene,path,pos,target,scale,w,h):
    cam=scene.camera;cam.location=pos;track(cam,target);cam.data.ortho_scale=scale
    scene.render.resolution_x=w;scene.render.resolution_y=h;scene.render.filepath=str(path)
    if '--force' in sys.argv or not path.exists():bpy.ops.render.render(write_still=True)

extracted={};report=[]
if '--only' in sys.argv and (OUT/'report.json').exists():
    report=[r for r in json.loads((OUT/'report.json').read_text()) if r['animal'] not in ANIMALS]
for animal in ANIMALS:
    source=ROOT/'source/blender/characters/SmoothBear'/('SmoothBear.blend' if animal=='Bear' else f'Hoods/BasicPlayerCapsule_WithWalk_{animal}_Ears.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source),use_scripts=False);rest()
    hood=bpy.data.objects['Hood'];deps=bpy.context.evaluated_depsgraph_get()
    mesh=bpy.data.meshes.new_from_object(hood.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
    obj=bpy.data.objects.new('Review_Hood_Source',mesh);bpy.context.scene.collection.objects.link(obj);obj.matrix_world=hood.matrix_world
    extracted[animal]={'matrix':[list(r) for r in hood.matrix_world],'hash':fingerprint(mesh),'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest()}
    bpy.data.libraries.write(str(OUT/(animal+'_source.blend')),{obj},fake_user=True)

for animal in ANIMALS:
    bpy.ops.wm.open_mainfile(filepath=str(BASE),use_scripts=False);rest();scene=bpy.context.scene
    for text in bpy.data.texts:text.use_module=False
    hood=bpy.data.objects['Hood'];before_body=fingerprint(bpy.data.objects['Body'].data)
    with bpy.data.libraries.load(str(OUT/(animal+'_source.blend')),link=False) as (src,dst):dst.objects=['Review_Hood_Source']
    imported=dst.objects[0];hood.data=imported.data.copy();bpy.data.objects.remove(imported,do_unlink=True)
    # All approved hood sources share the same authored local coordinates.
    assert fingerprint(hood.data)==extracted[animal]['hash']
    original=hood.data.materials[0].copy();original.name='MAT_Hood_Original_Comparison'
    original.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=linear('D4ECFF');original.use_fake_user=True
    add_uv(hood.data);fixed=knit_material();hood.data.materials.clear();hood.data.materials.append(fixed)
    for p in hood.data.polygons:p.material_index=0
    hood['review']='Straight vertical knit; original shape and UVMap preserved; new projection UV layers added.'
    cam=studio(scene)
    bpy.context.view_layer.update()
    top=max((hood.matrix_world@v.co).z for v in hood.data.vertices);target=(0,0,(1.28+top)*.5)
    height=top-1.28+.14;scale=max(1.08,height*1.18)
    render(scene,OUT/(animal+'_after.png'),(0,-5,target[2]+.10),target,scale,1000,1100)
    hood.data.materials[0]=original
    render(scene,OUT/(animal+'_before.png'),(0,-5,target[2]+.10),target,scale,1000,1100)
    hood.data.materials[0]=fixed
    render(scene,OUT/(animal+'_angle.png'),(2.2,-5,target[2]+.32),target,scale*1.12,1000,1100)
    cam.location=(0,-5,target[2]+.10);track(cam,target);cam.data.ortho_scale=scale
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.region_3d.view_location=target
                area.spaces.active.region_3d.view_distance=1.8
                area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)
    bpy.ops.object.select_all(action='DESELECT');hood.hide_set(False);hood.select_set(True);bpy.context.view_layer.objects.active=hood
    note=bpy.data.texts.new('STRAIGHT_KNIT_README')
    note.write('BLENDER REVIEW ONLY — '+animal+'\nOriginal hood geometry retained exactly. Current smooth body, compact shoes and brown eyes retained.\nNew UV layers Knit_Front_Straight and Knit_Side_Straight use shared, fixed-scale coordinates across the shell and ears. No polar mapping.\nMaterial softly blends front/back and side vertical rib normals to avoid stretched side walls. Rib spacing 0.022; relief 0.42.\nOriginal UVMap and MAT_Hood_Original_Comparison remain available.\nNo Unity assets modified. Procedural Blender material requires an export/bake step before Unity integration.\n')
    assert fingerprint(hood.data)==extracted[animal]['hash'];assert fingerprint(bpy.data.objects['Body'].data)==before_body
    bpy.ops.file.pack_all();scene.render.filepath='//'+animal+'_preview.png'
    file=DEST/f'BasicPlayerCapsule_{animal}_StraightKnit.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(file))
    report.append({'animal':animal,'file':str(file),'hood_geometry_unchanged':True,'body_geometry_unchanged':True,**extracted[animal]})
    (OUT/'report.json').write_text(json.dumps(report,indent=2))
    print('KNIT_REVIEW_SAVED '+str(file),flush=True)
