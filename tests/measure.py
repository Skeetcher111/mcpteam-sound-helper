import subprocess,re,sys,statistics
def m(f):
    o=subprocess.run(["ffmpeg","-nostats","-i",f,"-af","ebur128=peak=true","-f","null","-"],capture_output=True,text=True).stderr
    S=[float(x) for x in re.findall(r"S:\s*(-?[\d.]+)",o)]
    M=[float(x) for x in re.findall(r"M:\s*(-?[\d.]+)",o)]
    S=[s for s in S if s>-50]; M=[x for x in M if x>-50]
    I=re.findall(r"I:\s*(-?[\d.]+) LUFS",o)[-1]; LRA=re.findall(r"LRA:\s*([\d.]+) LU",o)[-1]; TP=re.findall(r"Peak:\s*(-?[\d.]+) dBFS",o)[-1]
    q=statistics.quantiles(S,n=20)
    return f"I={I} LUFS  LRA={LRA} LU  разброс S (5–95%)={q[0]:.1f}..{q[-1]:.1f} ({q[-1]-q[0]:.1f} дБ)  max M={max(M):.1f}  пик={TP} dBTP"
for f in sys.argv[1:]: print(f, m(f))
