# numpy 复刻 LSB_MemoryDevelop v02 片元着色器，用来核对 _ConeRect 映射与压暗（不是 Unity 渲染）
import numpy as np, sys
from PIL import Image
mask = np.asarray(Image.open(sys.argv[1]).convert('RGB')).astype(np.float32)/255.0
mask = mask[::-1]  # 贴图 v 向上
W,H = 960,540
cone_rect = (740/1920, 1-510/1080, 720/1080, W/H)
agg, dis, dimv, cells, soft, grainpx, grainamt = float(sys.argv[3]), float(sys.argv[4]), 0.42, 40.0, 0.15, 640.0, 0.10
S45=0.70710678
def hsh(p):
    p = (p*np.array([123.34,456.21]))%1.0
    p = p + (p*(p+45.32)).sum(-1,keepdims=True)
    return (p[...,0]*p[...,1])%1.0
def rot(p): return np.stack([S45*p[...,0]-S45*p[...,1], S45*p[...,0]+S45*p[...,1]],-1)
def irot(p): return np.stack([S45*p[...,0]+S45*p[...,1], -S45*p[...,0]+S45*p[...,1]],-1)
def ss(a,b,x): t=np.clip((x-a)/(b-a),0,1); return t*t*(3-2*t)
def conef(c): return 1-ss(0.5*(1-soft),0.5,np.linalg.norm(c-0.5,axis=-1))
def samp(uv,ch):
    uv=np.clip(uv,0,1); h,w=mask.shape[:2]
    x=np.clip((uv[...,0]*w).astype(int),0,w-1); y=np.clip((uv[...,1]*h).astype(int),0,h-1); return mask[y,x,ch]
ys,xs=np.mgrid[0:H,0:W]; uv=np.stack([(xs+0.5)/W,1-(ys+0.5)/H],-1)
c=(uv-np.array(cone_rect[:2]))*np.array([cone_rect[3],1.0])/cone_rect[2]+0.5
cs=1/cells; q=rot(c/cs); i=np.floor(q); f=q%1.0-0.5
h1,h2,h3=hsh(i),hsh(i+17.31),hsh(i+41.7)
cuv=irot(i+0.5)*cs; dens=samp(cuv,0)*conef(cuv)
a=ss(h1*0.75,h1*0.75+0.25,agg); k=ss(h2*0.65,h2*0.65+0.35,dis)
r=0.5*np.sqrt(dens)*1.08*a*(1-k)
drift=np.stack([h3-0.5,0.6+0.6*h2],-1)*(k*0.9)[...,None]
d=np.linalg.norm(f-irot(drift),axis=-1); dotA=(1-ss(r-0.07,r+0.07,d))*(r>=0.02)
cone=conef(c); g=samp(c,1)*cone; pix=np.floor(c*grainpx); n1,n2=hsh(pix),hsh(pix+7.7)
lineA=g*ss(n1*0.8,n1*0.8+0.2,agg)*(1-ss(n2*0.7,n2*0.7+0.3,dis))
alpha=np.clip(np.maximum(dotA,lineA),0,1)
dim=dimv*(1-cone); A=alpha+dim*(1-alpha)
ink=np.array([0.9098,0.7059,0.3529])
bg=np.zeros((H,W,3))+np.array([0.12,0.14,0.16])  # 占位夜景
out=ink*alpha[...,None]+bg*(1-A[...,None])
Image.fromarray((np.clip(out,0,1)**(1/2.2)*255).astype(np.uint8)).save(sys.argv[2])
print("alpha>0.5 px:", int((alpha>0.5).sum()), "cone center px:", cone_rect[0]*W, (1-cone_rect[1])*H)
