using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

// Reusable cover geometry. The same rounded surfaces and reflection are drawn
// in four batches instead of crossing into OpenGL for every tiny strip.
public partial class DepthWindow {
 sealed class FastCaseMesh {
  public RectangleF Bounds;public float Yaw,Pitch,Eye,Width,Alpha,H,HeadY;public int Height,EyeIndex,Plastic;
  public bool Ready;
  public readonly float[] Body=new float[4096],Art=new float[4096],Light=new float[4096],Edge=new float[64];
  public int BodyCount,ArtCount,LightCount;
  public readonly PointF[] Front=new PointF[32],Back=new PointF[32],Ring=new PointF[32],Quad=new PointF[4],Uv=new PointF[4],RefUv=new PointF[4];
  public float[] Cuts;public float CutRatio;
 }
 readonly Dictionary<Game,FastCaseMesh[]> fastCases=new Dictionary<Game,FastCaseMesh[]>();
 static readonly PointF[] caseUnitCorners=MakeCaseUnitCorners();
 static PointF[] MakeCaseUnitCorners(){var result=new PointF[32];for(int corner=0;corner<4;corner++)for(int step=0;step<8;step++){double a=(-180+corner*90+step*90/7.0)*Math.PI/180;result[corner*8+step]=new PointF((float)Math.Cos(a),(float)Math.Sin(a));}return result;}
 struct FastCaseProjection {
  public float CX,CY,CameraY,YC,YS,PC,PS,Eye,SX,SY,Offset;
  public PointF Project(float x,float y,float z){float xx=x*YC+z*YS,zz=-x*YS+z*YC,yy=y*PC-zz*PS;zz=y*PS+zz*PC;float k=900/(900-zz);return new PointF(Offset+(CX+(xx-Eye*zz/900)*k)*SX,(CameraY+(CY-CameraY+yy)*k)*SY);}
 }
 static void CaseBodyVertex(FastCaseMesh mesh,PointF p,Color color){int n=mesh.BodyCount;float a=color.A/255f;mesh.Body[n++]=p.X;mesh.Body[n++]=p.Y;mesh.Body[n++]=color.R/255f*a;mesh.Body[n++]=color.G/255f*a;mesh.Body[n++]=color.B/255f*a;mesh.Body[n++]=a;mesh.BodyCount=n;}
 static void CaseBodyTriangle(FastCaseMesh m,PointF a,PointF b,PointF c,Color color){CaseBodyVertex(m,a,color);CaseBodyVertex(m,b,color);CaseBodyVertex(m,c,color);}
 static void CaseTextureVertex(float[] target,ref int n,PointF p,PointF uv){target[n++]=p.X;target[n++]=p.Y;target[n++]=uv.X;target[n++]=uv.Y;}
 static void CaseTextureQuad(float[] target,ref int n,PointF[] p,PointF[] uv){CaseTextureVertex(target,ref n,p[0],uv[0]);CaseTextureVertex(target,ref n,p[1],uv[1]);CaseTextureVertex(target,ref n,p[2],uv[2]);CaseTextureVertex(target,ref n,p[0],uv[0]);CaseTextureVertex(target,ref n,p[2],uv[2]);CaseTextureVertex(target,ref n,p[3],uv[3]);}
 void DrawFastGameCase(Game game,RectangleF r,float h,float eye,int eyeIndex,float width,float alpha){
  FastCaseMesh[] pair;if(!fastCases.TryGetValue(game,out pair)){if(fastCases.Count>=96)fastCases.Clear();pair=new FastCaseMesh[2];fastCases[game]=pair;}FastCaseMesh mesh=pair[eyeIndex];if(mesh==null){mesh=new FastCaseMesh();pair[eyeIndex]=mesh;}
  PointF angles=CaseSurfaceAngles(game,h);Color plastic=CasePlastic(game);
  if(!mesh.Ready||mesh.Bounds!=r||mesh.Yaw!=angles.X||mesh.Pitch!=angles.Y||mesh.Eye!=eye||mesh.EyeIndex!=eyeIndex||mesh.Width!=width||mesh.Height!=ClientSize.Height||mesh.Alpha!=alpha||mesh.H!=h||mesh.HeadY!=headY||mesh.Plastic!=plastic.ToArgb()){
   mesh.Ready=true;mesh.Bounds=r;mesh.Yaw=angles.X;mesh.Pitch=angles.Y;mesh.Eye=eye;mesh.EyeIndex=eyeIndex;mesh.Width=width;mesh.Height=ClientSize.Height;mesh.Alpha=alpha;mesh.H=h;mesh.HeadY=headY;mesh.Plastic=plastic.ToArgb();BuildFastCase(mesh,plastic);
  }
  float reflectance=.75f+.25f*(1-(float)Math.Cos(angles.X)*(float)Math.Cos(angles.Y));
  gpu.CaseMeshDraw(mesh.Body,mesh.BodyCount,mesh.Art,mesh.ArtCount,mesh.Light,mesh.LightCount,mesh.Edge,Sprite(game),CaseReflection(),.94f+.06f*h,alpha,reflectance);
 }
 void BuildFastCase(FastCaseMesh mesh,Color plastic){
  RectangleF r=mesh.Bounds;float x=r.Width/2,y=r.Height/2,d=r.Height*.040f,radius=Math.Min(r.Width,r.Height)*.065f;int opacity=(int)(255*mesh.Alpha);
  var project=new FastCaseProjection{CX=r.X+x,CY=r.Y+y,CameraY=470+headY,YC=(float)Math.Cos(mesh.Yaw),YS=(float)Math.Sin(mesh.Yaw),PC=(float)Math.Cos(mesh.Pitch),PS=(float)Math.Sin(mesh.Pitch),Eye=mesh.Eye,SX=mesh.Width/1440,SY=ClientSize.Height/940f,Offset=mesh.EyeIndex*mesh.Width};
  mesh.BodyCount=mesh.ArtCount=mesh.LightCount=0;
  for(int i=0;i<32;i++){int corner=i/8;float cx=corner==0||corner==3?-x+radius:x-radius,cy=corner<2?-y+radius:y-radius;PointF unit=caseUnitCorners[i];var point=new PointF(cx+radius*unit.X,cy+radius*unit.Y);mesh.Ring[i]=point;mesh.Front[i]=project.Project(point.X,point.Y,d);mesh.Back[i]=project.Project(point.X,point.Y,-d);mesh.Edge[i*2]=mesh.Front[i].X;mesh.Edge[i*2+1]=mesh.Front[i].Y;}
  Color backColor=CaseShade(plastic,.6f,opacity);for(int i=1;i<31;i++)CaseBodyTriangle(mesh,mesh.Back[0],mesh.Back[i],mesh.Back[i+1],backColor);
  for(int i=0;i<32;i++){int j=(i+1)%32;float ex=mesh.Ring[j].X-mesh.Ring[i].X,ey=mesh.Ring[j].Y-mesh.Ring[i].Y,length=(float)Math.Sqrt(ex*ex+ey*ey);Color c=CaseShade(plastic,.78f+(length<.001f?0:(ex*.38f+ey*.18f)/length),opacity);CaseBodyTriangle(mesh,mesh.Back[i],mesh.Back[j],mesh.Front[j],c);CaseBodyTriangle(mesh,mesh.Back[i],mesh.Front[j],mesh.Front[i],c);}
  Color face=CaseShade(plastic,1.12f,opacity);for(int i=1;i<31;i++)CaseBodyTriangle(mesh,mesh.Front[0],mesh.Front[i],mesh.Front[i+1],face);
  float inset=Math.Max(2,r.Width*.019f),ix=x-inset,iy=y-inset,ir=Math.Max(1,radius-inset),ratio=ir/ix;
  if(mesh.Cuts==null||Math.Abs(mesh.CutRatio-ratio)>.000001f){mesh.Cuts=CaseArtCuts(ix,ir);mesh.CutRatio=ratio;}
  for(int i=0;i<mesh.Cuts.Length-1;i++){
   float u=mesh.Cuts[i],v=mesh.Cuts[i+1],a=-ix+2*ix*u,b=-ix+2*ix*v,ta=CaseTop(a,ix,iy,ir),tb=CaseTop(b,ix,iy,ir);
   mesh.Quad[0]=project.Project(a,ta,d+.12f);mesh.Quad[1]=project.Project(b,tb,d+.12f);mesh.Quad[2]=project.Project(b,-tb,d+.12f);mesh.Quad[3]=project.Project(a,-ta,d+.12f);
   mesh.Uv[0]=new PointF(u,(ta+iy)/(2*iy));mesh.Uv[1]=new PointF(v,(tb+iy)/(2*iy));mesh.Uv[2]=new PointF(v,(-tb+iy)/(2*iy));mesh.Uv[3]=new PointF(u,(-ta+iy)/(2*iy));
   for(int n=0;n<4;n++)mesh.RefUv[n]=CaseReflectionUv(mesh.Uv[n],mesh.Yaw,mesh.Pitch,mesh.Eye);
   CaseTextureQuad(mesh.Art,ref mesh.ArtCount,mesh.Quad,mesh.Uv);CaseTextureQuad(mesh.Light,ref mesh.LightCount,mesh.Quad,mesh.RefUv);
  }
 }
}
public sealed partial class GpuRenderer {
 [DllImport("opengl32.dll")]static extern void glDrawArrays(uint mode,int first,int count);
 public void ColoredTextureTriangles(Bitmap image,float[] vertices,int count){
  var pinned=GCHandle.Alloc(vertices,GCHandleType.Pinned);
  try{if(bindBuffer!=null)bindBuffer(0x8892,0);IntPtr p=pinned.AddrOfPinnedObject();glEnable(0x0DE1);glBindTexture(0x0DE1,Texture(image));glEnableClientState(0x8074);glEnableClientState(0x8078);glEnableClientState(0x8076);glVertexPointer(2,0x1406,32,p);glTexCoordPointer(2,0x1406,32,IntPtr.Add(p,8));glColorPointer(4,0x1406,32,IntPtr.Add(p,16));glDrawArrays(4,0,count);}
  finally{glDisableClientState(0x8074);glDisableClientState(0x8078);glDisableClientState(0x8076);pinned.Free();}
 }
 public void CaseMeshDraw(float[] body,int bodyCount,float[] art,int artCount,float[] light,int lightCount,float[] edge,Bitmap image,Bitmap reflection,float diffuse,float alpha,float reflectance){
  var b=GCHandle.Alloc(body,GCHandleType.Pinned);var a=GCHandle.Alloc(art,GCHandleType.Pinned);var l=GCHandle.Alloc(light,GCHandleType.Pinned);var e=GCHandle.Alloc(edge,GCHandleType.Pinned);
  try{
   if(bindBuffer!=null)bindBuffer(0x8892,0);
   glDisable(0x0DE1);glEnableClientState(0x8074);glEnableClientState(0x8076);glVertexPointer(2,0x1406,24,b.AddrOfPinnedObject());glColorPointer(4,0x1406,24,IntPtr.Add(b.AddrOfPinnedObject(),8));glDrawArrays(4,0,bodyCount/6);
   glDisableClientState(0x8076);glEnable(0x0DE1);glEnableClientState(0x8078);glBindTexture(0x0DE1,Texture(image));float tint=((int)(255*diffuse))/255f,opacity=((int)(255*alpha))/255f;glColor4f(tint*opacity,tint*opacity,tint*opacity,opacity);glVertexPointer(2,0x1406,16,a.AddrOfPinnedObject());glTexCoordPointer(2,0x1406,16,IntPtr.Add(a.AddrOfPinnedObject(),8));glDrawArrays(4,0,artCount/4);
   glBindTexture(0x0DE1,Texture(reflection));int lightAlpha=(int)(255*alpha*reflectance);float la=lightAlpha/255f;glColor4f(la,la,la,la);glVertexPointer(2,0x1406,16,l.AddrOfPinnedObject());glTexCoordPointer(2,0x1406,16,IntPtr.Add(l.AddrOfPinnedObject(),8));glDrawArrays(4,0,lightCount/4);
   glDisableClientState(0x8078);glDisable(0x0DE1);float ea=((int)(150*alpha))/255f;glColor4f(185/255f*ea,203/255f*ea,213/255f*ea,ea);glLineWidth(1);glVertexPointer(2,0x1406,8,e.AddrOfPinnedObject());glDrawArrays(2,0,edge.Length/2);
  }finally{glDisableClientState(0x8074);glDisableClientState(0x8076);glDisableClientState(0x8078);b.Free();a.Free();l.Free();e.Free();}
 }
}

