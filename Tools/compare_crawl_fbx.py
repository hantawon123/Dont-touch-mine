import bpy, sys, json
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, use_anim=True)
    body=next(o for o in bpy.data.objects if o.type=='MESH' and o.name=='Body')
    kb=body.data.shape_keys.key_blocks
    waist=kb.get('Crawl_Waist_Round')
    vals=[]
    if waist:
        vals=[(waist.data[i].co-body.data.vertices[i].co).length for i in range(len(body.data.vertices))]
    keyvals=[]
    for a in bpy.data.actions:
        for layer in a.layers:
            for strip in layer.strips:
                for slot in a.slots:
                    bag=strip.channelbag(slot)
                    if bag:
                        for fc in bag.fcurves:
                            if 'Crawl_Waist_Round' in fc.data_path:
                                keyvals=[float(k.co.y) for k in fc.keyframe_points]
    return {'verts':len(body.data.vertices),'waist_max':max(vals) if vals else None,'waist_mean':sum(vals)/len(vals) if vals else None,'waist_keys':keyvals[:3]+keyvals[-3:]}
paths=[sys.argv[-2],sys.argv[-1]]
print(json.dumps([load(p) for p in paths],indent=2),flush=True)
