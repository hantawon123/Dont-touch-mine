import bpy, sys, json
path = sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=path)
out=[]
for o in bpy.data.objects:
    if o.type=='MESH' and any(k in o.name.lower() for k in ['hood','ear','head']):
        bb=[o.matrix_world @ v.co for v in o.data.vertices]
        out.append({'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'dims':tuple(round(x,4) for x in o.dimensions),'loc':tuple(round(x,4) for x in o.location),'mods':[m.type for m in o.modifiers],'mats':[m.name if m else None for m in o.data.materials],'min':tuple(round(min(v[i] for v in bb),4) for i in range(3)),'max':tuple(round(max(v[i] for v in bb),4) for i in range(3))})
print('INSPECT', path, flush=True)
print(json.dumps(out,ensure_ascii=False,indent=2), flush=True)
