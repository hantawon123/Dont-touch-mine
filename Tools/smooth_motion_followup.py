"""Reuse the batch FBX helpers without running its earlier retiming jobs."""
from pathlib import Path
exec(compile(Path(__file__).with_name('refine_motion_batch.py').read_text(encoding='utf-8').split("\nr=load('Throw')")[0], 'batch_helpers', 'exec'))
from mathutils import Matrix
def blend(a,b,t):
 al,aq,asc=a.decompose();bl,bq,bsc=b.decompose();return Matrix.LocRotScale(al.lerp(bl,t),aq.slerp(bq,t),asc.lerp(bsc,t))
def sample(name):
 r=load(name);lo,hi=r.animation_data.action.frame_range;return [read(r,lo+i) for i in range(round(hi-lo)+1)]
carry=sample('Carry_Jump');jump=sample('Jump_Cute')
for i,(p,s) in enumerate(carry):
 for n in p:
  if n.endswith('.L') and any(x in n for x in ('Shoulder','Arm','Hand','Finger')):p[n]=jump[min(i,len(jump)-1)][0][n].copy()
export(None,'Carry_Jump',carry)
throw=sample('Throw')
# Symmetric pose filtering reduces frame-to-frame acceleration without extending the clip.
for iteration in range(3):
 previous=throw;throw=[previous[0]]
 for i in range(1,len(previous)-1):
  p={n:blend(m,blend(previous[i-1][0][n],previous[i+1][0][n],.5),.5) for n,m in previous[i][0].items()}
  throw.append((p,previous[i][1]))
 throw.append(previous[-1])
export(None,'Throw',throw)
hit=sample('Hit_Head')[-1];ko=sample('Knocked_Out');end=ko[-1];frames=[]
for i in range(len(ko)):
 t=min(1,i/40);t=t*t*(3-2*t)
 frames.append(({n:blend(m,end[0][n],t) for n,m in hit[0].items()},{n:v*(1-t)+end[1].get(n,0)*t for n,v in hit[1].items()}))
export(None,'Knocked_Out',frames)
print('PASS Carry left arm copied; Throw duration/endpoints preserved; KO starts at Hit_Head end')
