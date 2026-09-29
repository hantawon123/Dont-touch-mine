"""Build the Unity character from the approved smooth Blender source."""
import bpy
import json
import sys
import hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
sys.path.insert(0,str(Path(__file__).resolve().parent))
import stabilize_crawl_torso as torso
import hold_crawl_torso as fbx
from smooth_bear_skinning import capture_weights, restore_upper_body

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/supplied-bear-smooth/BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend')
OUT = ROOT / 'artifacts/smooth-bear-unity'
OUT.mkdir(parents=True,exist_ok=True)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),use_scripts=False)
    rig=bpy.data.objects['DGN_Armature']; body=bpy.data.objects['Body']
    source_weights=capture_weights(body)
    existing=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx'))
    imported=set(bpy.data.objects)-existing
    oldrig=next(o for o in imported if o.type=='ARMATURE')
    oldbody=next(o for o in imported if o.type=='MESH' and o.name.startswith('Body'))
    report={'new_world':list(map(list,rig.matrix_world)), 'old_world':list(map(list,oldrig.matrix_world)),
            'new_body_world':list(map(list,body.matrix_world)), 'old_body_world':list(map(list,oldbody.matrix_world)),
            'old_keys':[(k.name,len(k.data)) for k in oldbody.data.shape_keys.key_blocks],
            'new_keys':bool(body.data.shape_keys),'bones':[],
            'new_objects':[(o.name,o.parent.name if o.parent else None,o.parent_type,o.parent_bone) for o in existing],
            'old_objects':[(o.name,o.parent.name if o.parent else None,o.parent_type,o.parent_bone) for o in imported]}
    for b in rig.data.bones:
        old=oldrig.data.bones.get(b.name)
        if not old: report['bones'].append({'name':b.name,'missing':True});continue
        error=max(abs(x-y) for a,c in zip(b.matrix_local,old.matrix_local) for x,y in zip(a,c))
        world_error=max(abs(x-y) for a,c in zip(rig.matrix_world@b.matrix_local,oldrig.matrix_world@old.matrix_local) for x,y in zip(a,c))
        report['bones'].append({'name':b.name,'local_error':error,'world_error':world_error})
    (OUT/'source-inspection.json').write_text(json.dumps(report,indent=2))
    assert max(b['local_error'] for b in report['bones']) < .0001
    rig.animation_data_clear()
    rig.data.pose_position='REST'
    oldrig.data.pose_position='REST'
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    bpy.context.view_layer.update()
    # The approved base surface already has 48k vertices. Keep this dense,
    # smooth surface for skinning rather than adding a render subdivision.
    for mod in list(body.modifiers):
        if mod.type!='ARMATURE': body.modifiers.remove(mod)
    body.shape_key_add(name='Basis')
    oldbody.data.calc_loop_triangles()
    tris=[tuple(t.vertices) for t in oldbody.data.loop_triangles]
    coordinates=[v.co.copy() for v in oldbody.data.shape_keys.key_blocks[0].data]
    tree=BVHTree.FromPolygons(coordinates,tris,all_triangles=True)
    into_old=oldbody.matrix_world.inverted() @ body.matrix_world
    into_new=into_old.inverted().to_3x3()
    nearest=[]
    for v in body.data.vertices:
        p,n,index,distance=tree.find_nearest(into_old@v.co)
        tri=tris[index]
        bary=barycentric_transform(p,*[coordinates[i] for i in tri],Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
        nearest.append((tri,bary))
    for name in ('Belly_Breath','Crouch_Groin_Flat'):
        key=body.shape_key_add(name=name)
        oldkey=oldbody.data.shape_keys.key_blocks[name]
        deltas=[oldkey.data[i].co-coordinates[i] for i in range(len(coordinates))]
        for v,(tri,bary),base in zip(key.data,nearest,body.data.vertices):
            offset=sum((deltas[i]*w for i,w in zip(tri,bary)),Vector())
            v.co=base.co + into_new @ offset
    changed,core=torso.stabilize(body,rig)
    restore_upper_body(body,source_weights)
    body.data.materials.clear()
    body.data.materials.append(bpy.data.materials['MAT_Capsule_Character'])
    for poly in body.data.polygons:
        poly.material_index=0; poly.use_smooth=True
    for obj in imported: bpy.data.objects.remove(obj,do_unlink=True)
    meshes=[o for o in existing if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        if obj == body: continue
        bpy.context.view_layer.objects.active=obj
        obj.select_set(True)
        for mod in list(obj.modifiers):
            if mod.type!='ARMATURE': bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)
    # Explicit plain colors also give a useful material fallback on import.
    for name,color in [('MAT_Capsule_Character',(243/255,214/255,70/255,1)),('MAT_Hood',(212/255,236/255,1,1))]:
        mat=bpy.data.materials[name]
        linear=tuple(c/12.92 if c<.04045 else ((c+.055)/1.055)**2.4 for c in color[:3])+(1,)
        mat.diffuse_color=linear
        if mat.use_nodes:
            bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
            if bsdf:
                for link in list(bsdf.inputs['Base Color'].links): mat.node_tree.links.remove(link)
                bsdf.inputs['Base Color'].default_value=linear
    rig.data.pose_position='POSE'
    bpy.context.scene.frame_set(0)
    for obj in [rig]+meshes: obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    model=OUT/'SmoothBear.fbx'
    kwargs=fbx.FBX_KW.copy(); kwargs['bake_anim']=False; kwargs['embed_textures']=False; kwargs['path_mode']='AUTO'
    bpy.ops.export_scene.fbx(filepath=str(model),**kwargs)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SmoothBear_Unity.blend'),check_existing=False)
    report.update({'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'body_vertices':len(body.data.vertices),'stable_core_vertices':len(core),'reweighted_vertices':changed,
        'runtime_shapes':['Belly_Breath','Crouch_Groin_Flat'],'model_sha256':hashlib.sha256(model.read_bytes()).hexdigest()})
    newrig,newbody,_=fbx.import_fbx(model)
    assert len(newbody.data.vertices)==report['body_vertices']
    assert len(newbody.data.materials)==1
    assert len(newbody.data.shape_keys.key_blocks)==3
    assert set(newrig.data.bones.keys())=={b['name'] for b in report['bones']}
    report['roundtrip_vertices']=len(newbody.data.vertices)
    (OUT/'model-report.json').write_text(json.dumps(report,indent=2))
    print('MODEL_READY',report['body_vertices'],len(core),flush=True)

if __name__=='__main__': main()
