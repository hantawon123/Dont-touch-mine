import bpy, json, sys
path=sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, use_anim=True)
rows=[]
for a in bpy.data.actions:
    paths=[]; count=0; values={}
    for layer in a.layers:
        for strip in layer.strips:
            for slot in a.slots:
                try: bag=strip.channelbag(slot)
                except: bag=None
                if bag:
                    count += len(bag.fcurves)
                    paths.extend(fc.data_path for fc in bag.fcurves if 'shape_keys' in fc.data_path or 'key_blocks' in fc.data_path)
                    for fc in bag.fcurves:
                        if 'Crawl_Waist_Round' in fc.data_path:
                            values[fc.data_path]=[(float(k.co.x),float(k.co.y)) for k in fc.keyframe_points]
    rows.append({'action':a.name,'layers':len(a.layers),'slots':len(a.slots),'curves':count,'shape_paths':sorted(set(paths)),'waist_values':values})
for o in bpy.data.objects:
    if o.type=='MESH' and o.data.shape_keys:
        rows.append({'mesh':o.name,'keys':[k.name for k in o.data.shape_keys.key_blocks]})
print(json.dumps(rows,ensure_ascii=False,indent=2),flush=True)
