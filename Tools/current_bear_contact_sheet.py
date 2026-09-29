import sys
from pathlib import Path
from PIL import Image,ImageDraw
p=Path(sys.argv[1]);names=['Idle','Walk_Forward','Run_Forward','Jump','Crouch_Idle','Crawl_Forward','Carry_TwoHands','Punch','Pickup_Low','Fall','Stun_Idle','Prone_Idle']
out=Image.new('RGB',(400*4,450*3),(235,235,235));d=ImageDraw.Draw(out)
for i,n in enumerate(names):
 out.paste(Image.open(p/('pose_'+n+'.png')),(i%4*400,i//4*450+30));d.text((i%4*400+10,i//4*450+10),n,fill='black')
out.save(p/'CurrentBear_Motions.png')
