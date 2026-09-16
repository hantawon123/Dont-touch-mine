import bpy
from mathutils import Vector
from pathlib import Path

P=Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
bpy.ops.wm.open_mainfile(filepath=str(P/'BasicPlayerCapsule_Bear_CrawlCylinder.blend'), use_scripts=False)
b=bpy.data.objects['Body']; sk=b.data.shape_keys; base=sk.key_blocks['Basis']; waist=sk.key_blocks['Crawl_Waist_Round']
changed=0; max_add=0.0
for i,v in enumerate(b.data.vertices):
    x,y,z=v.co
    # Symmetric lateral fill through the waist and lower rib cage.
    ym=max(0.0,min(1.0,(y-0.52)/0.18))
    ym=ym*ym*(3-2*ym)
    yf=1.0-max(0.0,min(1.0,(y-1.13)/0.18))
    yf=yf*yf*(3-2*yf)
    xm=max(0.0,min(1.0,(abs(x)-0.10)/0.18))
    xm=xm*xm*(3-2*xm)
    mask=ym*yf*xm
    if mask<=0 or abs(x)<1e-6: continue
    add=0.055*mask*(1 if x>0 else -1)
    waist.data[i].co.x += add
    changed+=1; max_add=max(max_add,abs(add))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'BasicPlayerCapsule_Bear_CrawlCylinder.blend'))
print('SIDE_FILL',changed,max_add,flush=True)
