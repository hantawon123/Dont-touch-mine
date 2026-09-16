"""Draw the exact Unity catalog sRGB swatches, plus a hood reference sheet."""
import base64,json,html
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/customization'
OUT.mkdir(parents=True,exist_ok=True)
palette=json.loads((ROOT/'Assets/_Game/Content/Config/SmoothBearPalette.json').read_text(encoding='utf-8'))
font_path='C:/Windows/Fonts/malgun.ttf'
def font(size):return ImageFont.truetype(font_path,size)
canvas=Image.new('RGB',(1200,1000),'#F8F6F1');d=ImageDraw.Draw(canvas)
d.text((48,30),'캐릭터 컬러 팔레트',font=font(36),fill='#263142')
d.text((48,87),'기본 조합  ·  몸 #F3D646  +  후드 #D4ECFF',font=font(20),fill='#586170')
for category,title,top in [('body','BODY  ·  몸 색상',145),('hood','HOOD  ·  후드 색상',555)]:
    d.text((48,top),title,font=font(25),fill='#263142')
    for i,swatch in enumerate(palette[category]):
        x=48+(i%6)*186;y=top+58+(i//6)*168
        d.rounded_rectangle((x,y,x+169,y+89),radius=13,fill=swatch['hex'])
        d.text((x,y+97),swatch['label'],font=font(19),fill='#263142')
        d.text((x,y+127),swatch['hex'],font=font(17),fill='#667180')
canvas.save(OUT/'palette.png')
cards=''
for shape,label in [('Bear','곰'),('Cat','고양이'),('Dog','강아지'),('Rabbit','토끼')]:
    data=base64.b64encode((ROOT/f'Assets/_Game/Content/Characters/SmoothBear/Hoods/Icons/{shape}.png').read_bytes()).decode()
    cards+=f'<figure><img src="data:image/png;base64,{data}"><figcaption>{label}</figcaption></figure>'
rows=''
for category,title in [('body','몸 색상'),('hood','후드 색상')]:
    rows+=f'<section><h2>{title}</h2><div class="swatches">'
    for s in palette[category]:
        rows+=f'<button onclick="pick(this)" data-hex="{s["hex"]}" title="HEX 코드 선택"><span style="background:{s["hex"]}"></span><b>{html.escape(s["label"])}</b><code>{s["hex"]}</code></button>'
    rows+='</div></section>'
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>캐릭터 후드와 컬러 팔레트</title>
<style>body{background:#f8f6f1;color:#263142;font:16px "Malgun Gothic",sans-serif;margin:0}main{max-width:1120px;margin:auto;padding:40px 28px}h1{font-size:34px;margin:0 0 12px}p{color:#586170;line-height:1.8}.hoods{display:grid;grid-template-columns:repeat(4,1fr);gap:20px}figure{margin:0;background:#e6ebef;border-radius:20px;padding:18px;text-align:center}figure img{width:100%}figcaption{font-weight:bold;margin-top:10px}section{margin-top:38px}.swatches{display:grid;grid-template-columns:repeat(6,1fr);gap:18px}button{border:0;background:transparent;padding:0;text-align:left;cursor:pointer;color:inherit}button span{display:block;height:82px;border-radius:12px;margin-bottom:10px}button b,code{display:block;line-height:1.7}code{color:#667180}#selected{margin-top:24px;padding:14px 20px;background:#fff;border-radius:12px}input{border:0;background:transparent;color:#263142;font:inherit;width:100px}@media(max-width:700px){.swatches{grid-template-columns:repeat(3,1fr)}.hoods{grid-template-columns:repeat(2,1fr)}}</style>
<main><h1>작은 귀, 나만의 색</h1><p>곰 · 고양이 · 강아지 · 토끼 후드<br>몸과 후드에 각각 12색. 기본 조합은 레몬 몸 <b>#F3D646</b>과 아이스 블루 후드 <b>#D4ECFF</b>입니다.</p>
<div class="hoods">'''+cards+'</div>'+rows+'''<div id="selected">선택한 HEX 코드 <input id="hex" value="#F3D646" readonly> · 색상을 누르면 코드를 선택합니다.</div>
<p>추천 조합 · 레몬 + 아이스 블루 / 코랄 + 크림 / 민트 + 아이보리 / 라벤더 + 버터<br>위 후드 이미지는 현재 캐릭터를 Unity에서 렌더링한 모습입니다.</p></main>
<script>function pick(button){const input=document.querySelector('#hex');input.value=button.dataset.hex;input.focus();input.select();}</script></html>'''
(OUT/'palette.html').write_text(page,encoding='utf-8')
print(OUT/'palette.png')
