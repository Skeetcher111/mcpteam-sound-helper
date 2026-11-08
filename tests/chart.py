import subprocess,re,sys
from PIL import Image,ImageDraw,ImageFont
def series(f):
    o=subprocess.run(["ffmpeg","-nostats","-i",f,"-af","ebur128","-f","null","-"],capture_output=True,text=True).stderr
    v=[(float(t),float(s)) for t,s in re.findall(r"t:\s*([\d.]+).*?S:\s*(-?[\d.]+)",o)]
    return [s for t,s in v][::10]  # 1 value per second
a=series("tests/tos.wav"); b=series("tests/tos_out.wav"); n=min(len(a),len(b))
W,H=1600,720; L,R,T,B=110,60,120,90
F=lambda s:ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf",s); FB=lambda s:ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf",s)
lo,hi=-40,-4
X=lambda i:L+(W-L-R)*i/(n-1); Y=lambda v:T+(H-T-B)*(hi-max(lo,min(hi,v)))/(hi-lo)
BLUE,ORANGE,INK,MUTED,GRID=(47,111,208),(217,101,0),(30,30,30),(110,110,110),(228,228,228)
def frame(k):
    im=Image.new("RGB",(W,H),"white"); d=ImageDraw.Draw(im)
    d.text((L,28),"Фильм Tears of Steel (12 мин): громкость до и после «Ровно»",fill=INK,font=FB(34))
    d.text((L,74),"Кратковременная громкость, LUFS (3 с). Чем ровнее линия, тем реже тянешься к регулятору.",fill=MUTED,font=F(22))
    for v in range(lo,hi+1,6):
        d.line((L,Y(v),W-R,Y(v)),fill=GRID,width=1); d.text((L-14,Y(v)),f"{v}",fill=MUTED,font=F(20),anchor="rm")
    for m in range(0,n,120): d.text((X(m),H-B+14),f"{m//60} мин",fill=MUTED,font=F(20),anchor="ma")
    for s,c in ((a,BLUE),(b,ORANGE)):
        pts=[(X(i),Y(s[i])) for i in range(min(k,n))]
        if len(pts)>1: d.line(pts,fill=c,width=2,joint="curve")
    d.text((W-R,T-6),"разброс громкости: 23 дБ → 9 дБ",fill=INK,font=F(22),anchor="rs")
    lx=W-R-420; d.line((lx-40,H-28,lx-10,H-28),fill=BLUE,width=4); d.text((lx,H-28),"до (как есть)",fill=INK,font=F(22),anchor="lm")
    lx2=lx+190; d.line((lx2-40,H-28,lx2-10,H-28),fill=ORANGE,width=4); d.text((lx2,H-28),"после «Ровно»",fill=INK,font=F(22),anchor="lm")
    return im
frame(n).save("docs/chart.png")
fr=[frame(int(n*i/40)).resize((800,360),Image.LANCZOS) for i in range(1,41)]
fr+= [fr[-1]]*12
fr[0].save("docs/demo.gif",save_all=True,append_images=fr[1:],duration=80,loop=0,optimize=True)
