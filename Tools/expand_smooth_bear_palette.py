"""Write the approved palette with a pale lilac and eight vivid colour options.
Hue families: yellow, green, blue, purple, pink, red; then light-to-dark neutrals.
Corresponding existing IDs are retained for saved selections.
"""
import json
from pathlib import Path
swatches = [
 ('orange_vivid','선명한 주황','#FF8A24'),
 ('yellow','노랑','#FFCB77'),('yellow_vivid','선명한 노랑','#FFE033'),
 ('lime','라임 연두','#D9ED92'),
 ('sage','세이지 연두','#CCD5AE'),('mint','민트 연두','#95D5B2'),
 ('green_vivid','선명한 초록','#27D85F'),('green','초록','#43AA8B'),
 ('cyan_vivid','선명한 청록','#20CDF5'),('sky','하늘','#BDE0FE'),
 ('periwinkle','하늘 보라','#BBD0FF'),('blue_vivid','선명한 파랑','#3478FF'),('blue','파랑','#0466C8'),
 ('navy','남색','#002855'),('slate','회빛 보라','#8E9AAF'),
 ('lilac_light','라일락 크림','#E4CCFF'),('lavender','연보라','#CDB4DB'),
 ('purple','보라','#9381FF'),('purple_vivid','선명한 보라','#8B4DFF'),
 ('pink','연분홍','#FFCAD4'),('pink_vivid','선명한 핑크','#FF4FA3'),('rose','진분홍','#DA627D'),
 ('red_vivid','선명한 빨강','#FF3B3B'),('red','빨강','#E63946'),('white','오프화이트','#F8F9FA'),
 ('grey','라이트 그레이','#CED4DA'),('charcoal','차콜','#495057'),
 ('black','딥 차콜','#212529')]
legacy = {
 'body': {'yellow':'lemon','lime':'apple','sage':'pistachio','sky':'skyblue'},
 'hood': {'yellow':'butter','lime':'apple','sky':'ice','white':'ivory'},
 'shoes': {'lime':'apple','pink':'cotton','sky':'babyblue'}}
palette = {category: [dict(id=category+'_'+legacy[category].get(key,key),label=label,hex=h)
 for key,label,h in swatches] for category in legacy}
for colors in palette.values():
 assert len(colors)==len({c['id'] for c in colors})==len({c['hex'] for c in colors})==28
path=Path(__file__).resolve().parents[1]/'Assets/_Game/Content/Config/SmoothBearPalette.json'
path.write_text(json.dumps(palette,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print({k:len(v) for k,v in palette.items()})
