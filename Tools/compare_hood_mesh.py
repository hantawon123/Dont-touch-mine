import bpy, hashlib, json

def inspect(path):
    bpy.ops.wm.open_mainfile(filepath=path)
    o=bpy.data.objects.get('Hood')
    if not o: raise RuntimeError('Hood not found')
    vals=[]
    for v in o.data.vertices:
        vals.extend(round(float(c),8) for c in v.co)
    raw=','.join(f'{x:.8f}' for x in vals).encode()
    return {'verts':len(o.data.vertices),'faces':len(o.data.polygons),'dims':tuple(round(x,8) for x in o.dimensions),'hash':hashlib.sha256(raw).hexdigest(),'materials':[m.name for m in o.data.materials]}

paths=[r'C:\Users\SSAFY\Desktop\게임프로젝트_캐릭터블렌드\BasicPlayerCapsule_WithWalk_Bear_Ears.blend',r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions\BasicPlayerCapsule_Bear_CrawlCylinder.blend']
results=[]
for p in paths: results.append((p,inspect(p)))
open(r'C:\Users\SSAFY\barleymilk\S15P21D205\hood_compare.json','w',encoding='utf-8').write(json.dumps(results,ensure_ascii=False,indent=2))
print(json.dumps(results,ensure_ascii=False,indent=2),flush=True)
