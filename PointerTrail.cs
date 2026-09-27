using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public partial class DepthWindow {
 sealed class PointerSpark {
  public float X,Y,Z,VX,VY,VZ,Age,Life,Size,Phase;
 }
 readonly List<PointerSpark> pointerSparks=new List<PointerSpark>();
 readonly Random sparkleRandom=new Random(7319);
 Bitmap sparkleSprite;
 PointF trailSample;float trailSampleZ,trailVelocityX,trailVelocityY,trailVelocityZ,trailCarry;bool haveTrailSample;
 const int MaxPointerSparks=56;

 void ResetPointerTrail(){pointerSparks.Clear();haveTrailSample=false;trailCarry=0;trailVelocityX=trailVelocityY=trailVelocityZ=0;}
 void InvalidatePointerTrail(){
  if(pointerSparks.Count==0)return;float left=float.MaxValue,top=float.MaxValue,right=float.MinValue,bottom=float.MinValue;
  foreach(var p in pointerSparks){float radius=p.Size+12;left=Math.Min(left,p.X-radius);top=Math.Min(top,p.Y-radius);right=Math.Max(right,p.X+radius);bottom=Math.Max(bottom,p.Y+radius);}
  if(left<=right)InvalidateLogical(RectangleF.FromLTRB(left,top,right,bottom));
 }
 void UpdatePointerTrail(double elapsed){
  float dt=(float)Math.Max(0,Math.Min(.05,elapsed));InvalidatePointerTrail();
  for(int i=pointerSparks.Count-1;i>=0;i--){
   PointerSpark p=pointerSparks[i];p.Age+=dt;if(p.Age>=p.Life){pointerSparks.RemoveAt(i);continue;}
   p.X+=p.VX*dt;p.Y+=p.VY*dt;p.Z=Math.Max(-120,Math.Min(420,p.Z+p.VZ*dt));
   float damping=(float)Math.Exp(-dt*4.6);p.VX*=damping;p.VY*=damping;p.VZ*=damping;p.VY-=4*dt;
  }
  bool canEmit=PointerVisible&&!libraryMasked&&introSince<0&&revealSince<0&&activeGame==null;
  if(!canEmit){haveTrailSample=false;InvalidatePointerTrail();return;}
  float z=PointerDepth;
  if(!haveTrailSample){trailSample=pointer;trailSampleZ=z;haveTrailSample=true;InvalidatePointerTrail();return;}
  float dx=pointer.X-trailSample.X,dy=pointer.Y-trailSample.Y,dz=z-trailSampleZ;
  float rawVX=dt>0?dx/dt:0,rawVY=dt>0?dy/dt:0,rawVZ=dt>0?dz/dt:0;
  float velocityBlend=1-(float)Math.Exp(-dt*14);trailVelocityX+=(rawVX-trailVelocityX)*velocityBlend;trailVelocityY+=(rawVY-trailVelocityY)*velocityBlend;trailVelocityZ+=(rawVZ-trailVelocityZ)*velocityBlend;
  float distance=(float)Math.Sqrt(dx*dx+dy*dy+dz*dz*.12f),speed=(float)Math.Sqrt(trailVelocityX*trailVelocityX+trailVelocityY*trailVelocityY);
  trailCarry+=distance;float spacing=Math.Max(8,15-Math.Min(6,speed/260));int emit=Math.Min(8,(int)(trailCarry/spacing));
  if(emit>0){trailCarry-=emit*spacing;for(int n=0;n<emit;n++){
   float t=(n+1f)/(emit+1f),jitter=(float)(sparkleRandom.NextDouble()*2-1),side=(float)(sparkleRandom.NextDouble()*2-1);
   float length=Math.Max(1,(float)Math.Sqrt(dx*dx+dy*dy)),nx=-dy/length,ny=dx/length;
   var spark=new PointerSpark{
    X=trailSample.X+dx*t+nx*side*3,Y=trailSample.Y+dy*t+ny*side*3,Z=trailSampleZ+dz*t,
    VX=trailVelocityX*.075f+nx*jitter*12,VY=trailVelocityY*.075f+ny*jitter*12,VZ=trailVelocityZ*.08f,
    Life=.42f+(float)sparkleRandom.NextDouble()*.26f,Size=2.2f+(float)sparkleRandom.NextDouble()*2.8f,Phase=(float)sparkleRandom.NextDouble()*6.283f
   };
   pointerSparks.Add(spark);if(pointerSparks.Count>MaxPointerSparks)pointerSparks.RemoveAt(0);
  }}
  trailSample=pointer;trailSampleZ=z;InvalidatePointerTrail();
 }
 Bitmap SparkleSprite(){
  if(sparkleSprite!=null)return sparkleSprite;sparkleSprite=new Bitmap(48,48,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(sparkleSprite)){g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(24,24);
   using(var glow=new PathGradientBrush(new[]{new PointF(-18,0),new PointF(0,-18),new PointF(18,0),new PointF(0,18)})){glow.CenterColor=Color.FromArgb(105,145,225,255);glow.SurroundColors=new[]{Color.Transparent,Color.Transparent,Color.Transparent,Color.Transparent};g.FillEllipse(glow,-18,-18,36,36);}
   using(var pen=new Pen(Color.FromArgb(178,205,246,255),1.4f)){g.DrawLine(pen,-7,0,7,0);g.DrawLine(pen,0,-7,0,7);}
   using(var center=new SolidBrush(Color.FromArgb(225,238,253,255)))g.FillEllipse(center,-1.7f,-1.7f,3.4f,3.4f);
  }return sparkleSprite;
 }
 float SparkAlpha(PointerSpark p){
  float life=Math.Min(1,p.Age/p.Life),head=(float)Math.Pow(1-life,1.55),twinkle=.82f+.18f*(float)Math.Sin(p.Phase+p.Age*21);
  // Begin bright beside the star, then quickly soften into the older tail.
  return Math.Max(0,head*twinkle*.92f);
 }
 void DrawPointerTrail(Graphics g,int eye,int count,float width){
  if(pointerSparks.Count==0)return;Bitmap image=SparkleSprite();
  foreach(var p in pointerSparks){float alpha=SparkAlpha(p);if(alpha<=.01f)continue;float freshness=1-Math.Min(1,p.Age/p.Life),pulse=1+.18f*(float)Math.Sin(p.Phase+p.Age*16),size=p.Size*pulse*(2.8f+1.8f*freshness);
   float x=p.X+StereoMath.Parallax(Eye(eye,count)-headX,p.Z),y=p.Y;RectangleF r=new RectangleF(x-size/2,y-size/2,size,size);
   if(gpu!=null)gpu.Image(image,Pixels(r,eye,width),alpha);
   else using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(image,Rectangle.Round(r),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}
  }
 }
 void DisposePointerTrail(){if(sparkleSprite!=null){if(gpu!=null)gpu.Forget(sparkleSprite);sparkleSprite.Dispose();sparkleSprite=null;}pointerSparks.Clear();}
}
