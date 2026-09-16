import bpy, json
path=r'C:\Users\SSAFY\Desktop\게임프로젝트_캐릭터블렌드\BasicPlayerCapsule_WithWalk_Bear_Ears.blend'
bpy.ops.wm.open_mainfile(filepath=path)
out=[]
for o in bpy.data.objects:
    if o.type=='MESH':
        s=o.name.lower()
        if any(k in s for k in ['hood','ear','head']):
            bb=[o.matrix_world @ v.co for v in o.data.vertices]
            out.append({'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'dims':tuple(round(x,4) for x in o.dimensions),'mats':[m.name if m else None for m in o.data.materials],'min':tuple(round(min(v[i] for v in bb),4) for i in range(3)),'max':tuple(round(max(v[i] for v in bb),4) for i in range(3))})
open(r'C:\Users\SSAFY\barleymilk\S15P21D205\source_inspect.json','w',encoding='utf-8').write(json.dumps(out,ensure_ascii=False,indent=2))
print('DONE',len(out),flush=True)
