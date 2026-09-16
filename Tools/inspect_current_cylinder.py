import bpy, json
path=r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions\BasicPlayerCapsule_Bear_CrawlCylinder.blend'
bpy.ops.wm.open_mainfile(filepath=path)
out=[]
for o in bpy.data.objects:
    if o.type=='MESH' and any(k in o.name.lower() for k in ['hood','ear','head']):
        bb=[o.matrix_world @ v.co for v in o.data.vertices]
        out.append({'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'dims':tuple(round(x,4) for x in o.dimensions),'mats':[m.name if m else None for m in o.data.materials],'min':tuple(round(min(v[i] for v in bb),4) for i in range(3)),'max':tuple(round(max(v[i] for v in bb),4) for i in range(3))})
open(r'C:\Users\SSAFY\barleymilk\S15P21D205\current_inspect.json','w',encoding='utf-8').write(json.dumps(out,ensure_ascii=False,indent=2))
print('DONE',len(out),flush=True)
