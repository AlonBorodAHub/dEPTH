using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public partial class DepthWindow {
 const int DustCount=208;
 sealed class DustMote {public float X,Y,Phase,Life,Radius,Sway,HeightOffset;}
 struct DustView {public float X,Y,Z,Size,Alpha;}
 DustMote[] dustMotes;Bitmap dustSprite;double dustTime;
 bool compareLegacyDust=false;
 DustView[][] dustViewCache;double dustViewTime=-1;float dustViewHeadX,dustViewHeadY,dustViewEye0,dustViewEye1;int dustViewCount;
 float[] dustBatchVertices;
 void PrepareDustViews(int count){
  float e0=Eye(0,count),e1=count==2?Eye(1,count):0;
  if(dustViewCache!=null&&dustViewTime==dustTime&&dustViewHeadX==headX&&dustViewHeadY==headY&&dustViewCount==count&&dustViewEye0==e0&&dustViewEye1==e1)return;
  if(dustViewCache==null)dustViewCache=new[]{new DustView[DustCount],new DustView[DustCount]};
  for(int eye=0;eye<count;eye++)for(int i=0;i<DustCount;i++)dustViewCache[eye][i]=DustSample(i,dustTime,Eye(eye,count));
  dustViewTime=dustTime;dustViewHeadX=headX;dustViewHeadY=headY;dustViewCount=count;dustViewEye0=e0;dustViewEye1=e1;
 }
 static void DustVertex(float[] target,ref int at,float x,float y,float u,float v,float alpha){target[at++]=x;target[at++]=y;target[at++]=u;target[at++]=v;target[at++]=alpha;target[at++]=alpha;target[at++]=alpha;target[at++]=alpha;}
 void EnsureDust(){
  if(dustMotes!=null)return;var random=new Random(46831);dustMotes=new DustMote[DustCount];
  for(int i=0;i<DustCount;i++)dustMotes[i]=new DustMote{X=(float)(random.NextDouble()*2800-1400),Y=310+(float)random.NextDouble()*200,Phase=(float)random.NextDouble(),Life=65+(float)random.NextDouble()*60,Radius=.65f+(float)random.NextDouble()*1.1f,Sway=12+(float)random.NextDouble()*20,HeightOffset=(float)(random.NextDouble()*600-300)};
 }
 void UpdateDust(double dt){
  if(panel!=""||libraryMasked||activeGame!=null)return;
  EnsureDust();dustTime+=Math.Max(0,Math.Min(.05,dt));InvalidateLogical(new RectangleF(0,196,1440,692));
 }
 static float DustFade(float value){value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
 DustView DustSample(int index,double time,float eye){
  EnsureDust();DustMote p=dustMotes[index];float t=(float)((time/p.Life+p.Phase)%1);
  float z=-900+1260*(float)Math.Pow(t,1.7),k=900/(900-z);
  double phase=p.Phase*Math.PI*2;
  float drift=p.Sway*(float)Math.Sin(time*.13+phase)+7*(float)Math.Sin(time*.061+phase*1.7);
  float x=720+(p.X+drift)*k+StereoMath.Parallax(eye-headX*.5f,z);
  float y=470+p.HeightOffset+(p.Y*(1-t)-80*t+8*(float)Math.Sin(time*.095+phase))*k+StereoMath.Parallax(headY*.55f,z);
  float fade=DustFade(t/.10f)*DustFade((1-t)/.13f)*DustFade((y-196)/40)*DustFade((886-y)/45)*DustFade((x-20)/45)*DustFade((1420-x)/45);
  return new DustView{X=x,Y=y,Z=z,Size=p.Radius*k*6.4f,Alpha=fade*(.38f+.32f*t)};
 }
 static int DustLayer(float z){return z< -650?0:z<100?1:z<235?2:3;}
 Bitmap DustSprite(){
  if(dustSprite!=null)return dustSprite;const int size=64;var pixels=new byte[size*size*4];
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){
   double dx=(x+.5-size/2)/(size/2),dy=(y+.5-size/2)/(size/2),r2=dx*dx+dy*dy;
   byte a=(byte)(r2>=1?0:240*Math.Exp(-r2*4.5)*DustFade((float)((1-r2)*3)));
   int at=(y*size+x)*4;pixels[at]=pixels[at+1]=pixels[at+2]=pixels[at+3]=a;
  }
  dustSprite=new Bitmap(size,size,PixelFormat.Format32bppPArgb);var data=dustSprite.LockBits(new Rectangle(0,0,size,size),ImageLockMode.WriteOnly,dustSprite.PixelFormat);
  try{Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{dustSprite.UnlockBits(data);}return dustSprite;
 }
 void DrawDust(Graphics g,int layer,int eye,int count,float width){
  if(panel!=""||libraryMasked||activeGame!=null)return;
  Bitmap image=DustSprite();float entrance=DustFade((float)dustTime/2),aspect=ClientSize.Width*940f/(Math.Max(1,ClientSize.Height)*1440f);
  if(!compareLegacyDust)PrepareDustViews(count);
  bool batch=gpu!=null&&!compareLegacyDust;int at=0;if(batch&&dustBatchVertices==null)dustBatchVertices=new float[DustCount*6*8];
  for(int i=0;i<DustCount;i++){
   DustView p=compareLegacyDust?DustSample(i,dustTime,Eye(eye,count)):dustViewCache[eye][i];if(DustLayer(p.Z)!=layer||p.Alpha*entrance<.006f)continue;
   var r=new RectangleF(p.X-p.Size/aspect/2,p.Y-p.Size/2,p.Size/aspect,p.Size);float alpha=p.Alpha*entrance;
   if(batch){RectangleF pixels=Pixels(r,eye,width);
    DustVertex(dustBatchVertices,ref at,pixels.Left,pixels.Top,0,0,alpha);DustVertex(dustBatchVertices,ref at,pixels.Right,pixels.Top,1,0,alpha);DustVertex(dustBatchVertices,ref at,pixels.Right,pixels.Bottom,1,1,alpha);
    DustVertex(dustBatchVertices,ref at,pixels.Left,pixels.Top,0,0,alpha);DustVertex(dustBatchVertices,ref at,pixels.Right,pixels.Bottom,1,1,alpha);DustVertex(dustBatchVertices,ref at,pixels.Left,pixels.Bottom,0,1,alpha);
   }else if(gpu!=null)gpu.Image(image,Pixels(r,eye,width),alpha);
   else using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(image,new[]{new PointF(r.Left,r.Top),new PointF(r.Right,r.Top),new PointF(r.Left,r.Bottom)},new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel,attributes);}
  }
  if(batch&&at>0)gpu.ColoredTextureTriangles(image,dustBatchVertices,at/8);
 }
 void DisposeDust(){dustViewCache=null;dustBatchVertices=null;dustViewTime=-1;if(dustSprite!=null){if(gpu!=null)gpu.Forget(dustSprite);dustSprite.Dispose();dustSprite=null;}dustMotes=null;}
}





