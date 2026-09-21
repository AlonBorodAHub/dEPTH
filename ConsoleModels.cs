using System;
using System.Collections.Generic;
using System.Drawing;

// Original, code-built console miniatures. Coordinates and lighting are shared
// by the GPU compositor and software fallback; no downloaded assets are needed.
public static class ConsoleModels {
 public struct V {public float X,Y,Z;public V(float x,float y,float z){X=x;Y=y;Z=z;}}
 public sealed class Face {public V[] Vertices;public Color Color;}
 public sealed class Polygon {public PointF[] Points;public float[] Depths;public Color Color;public float Depth;}
 static readonly Dictionary<string,List<Face>> cache=new Dictionary<string,List<Face>>();
 static readonly Color dark=Color.FromArgb(29,36,46), black=Color.FromArgb(12,20,28), silver=Color.FromArgb(198,211,220), blue=Color.FromArgb(36,156,205);
 public static string Key(string platform){
  string p=(platform??"").Trim().ToLowerInvariant().Replace("nintendo ","");
  if(p=="gamecube"||p=="gc"||p=="ngc")return "GameCube";
  if(p=="64"||p=="n64")return "Nintendo 64";
  if(p=="3ds"||p=="new 3ds")return "3DS";
  if(p=="ds"||p=="nds")return "DS";
  if(p=="wii")return "Wii";if(p=="wii u"||p=="wiiu")return "Wii U";
  if(p=="switch")return "Switch";if(p=="dreamcast")return "Dreamcast";
  if(p=="ps2"||p=="playstation 2")return "PS2";if(p=="ps3"||p=="playstation 3")return "PS3";
  return "PC";
 }
 static void Quad(List<Face> m,Color c,params V[] v){m.Add(new Face{Vertices=v,Color=c});}
 static void Box(List<Face> m,Color c,float x,float y,float z,float w,float h,float d){
  float l=x-w/2,r=x+w/2,b=y-h/2,t=y+h/2,f=z+d/2,k=z-d/2;
  Quad(m,c,new V(l,b,f),new V(r,b,f),new V(r,t,f),new V(l,t,f));
  Quad(m,c,new V(r,b,k),new V(l,b,k),new V(l,t,k),new V(r,t,k));
  Quad(m,c,new V(l,b,k),new V(l,b,f),new V(l,t,f),new V(l,t,k));
  Quad(m,c,new V(r,b,f),new V(r,b,k),new V(r,t,k),new V(r,t,f));
  Quad(m,c,new V(l,t,f),new V(r,t,f),new V(r,t,k),new V(l,t,k));
  Quad(m,c,new V(l,b,k),new V(r,b,k),new V(r,b,f),new V(l,b,f));
 }
 // Cylinder on Y (lid/button) or Z (socket/stick), including actual side walls.
 static void Disc(List<Face> m,Color c,float x,float y,float z,float radius,float thickness,bool front){
  for(int i=0;i<32;i++){
   double a=i*Math.PI/16,b=(i+1)*Math.PI/16;
   float ax=(float)Math.Cos(a)*radius,ay=(float)Math.Sin(a)*radius,bx=(float)Math.Cos(b)*radius,by=(float)Math.Sin(b)*radius;
   V p=front?new V(x+ax,y+ay,z+thickness/2):new V(x+ax,y+thickness/2,z-ay);
   V q=front?new V(x+bx,y+by,z+thickness/2):new V(x+bx,y+thickness/2,z-by);
   V center=front?new V(x,y,z+thickness/2):new V(x,y+thickness/2,z);
   Quad(m,c,center,p,q);
   V pk=front?new V(p.X,p.Y,z-thickness/2):new V(p.X,y-thickness/2,p.Z);
   V qk=front?new V(q.X,q.Y,z-thickness/2):new V(q.X,y-thickness/2,q.Z);
   Quad(m,c,p,pk,qk,q);
  }
 }
 static void Pad(List<Face> m,float x,float y,float z){Box(m,black,x,y,z,.09f,.28f,.025f);Box(m,black,x,y,z,.28f,.09f,.026f);}
 static void Buttons(List<Face> m,float x,float y,float z){for(int i=0;i<4;i++){double a=i*Math.PI/2;Disc(m,dark,x+(float)Math.Cos(a)*.13f,y+(float)Math.Sin(a)*.13f,z,.047f,.03f,true);}}
 static void Screen(List<Face> m,float x,float y,float z,float w,float h){Box(m,black,x,y,z,w,h,.025f);Box(m,Color.FromArgb(47,88,108),x,y,z+.017f,w*.88f,h*.82f,.015f);}
 public static List<Face> Mesh(string platform){
  string key=Key(platform);List<Face> m;if(cache.TryGetValue(key,out m))return m;m=new List<Face>();
  if(key=="GameCube"){
   Color purple=Color.FromArgb(92,80,161);Box(m,purple,0,0,0,1.65f,1.35f,1.6f);Box(m,dark,0,-.52f,.815f,1.5f,.26f,.035f);
   Disc(m,black,0,.687f,0,.61f,.025f,false);Disc(m,silver,0,.708f,0,.17f,.013f,false);
   for(int i=0;i<4;i++)Disc(m,black,-.55f+i*.365f,-.2f,.82f,.115f,.04f,true);
   Box(m,dark,0,.1f,-1.01f,1.24f,.15f,.15f);Box(m,dark,-.55f,.1f,-.88f,.14f,.15f,.35f);Box(m,dark,.55f,.1f,-.88f,.14f,.15f,.35f);
   Disc(m,silver,-.65f,.69f,.52f,.09f,.035f,false);Disc(m,silver,.65f,.69f,.52f,.09f,.035f,false);
  }else if(key=="3DS"||key=="DS"){
   Color shell=key=="3DS"?Color.FromArgb(46,147,171):silver;
   Box(m,shell,0,-.52f,.2f,2.12f,.15f,1.2f);Box(m,black,0,-.431f,.2f,1.94f,.016f,1.02f);
   // Upper screen opens 112 degrees from the base, with controls on the base.
   var lid=new List<Face>();Box(lid,shell,0,.52f,0,2.12f,1.17f,.13f);Screen(lid,0,.52f,.077f,1.52f,.85f);
   Disc(lid,black,0,1.025f,.082f,.032f,.018f,true);
   foreach(Face face in lid){for(int i=0;i<face.Vertices.Length;i++){V v=face.Vertices[i];face.Vertices[i]=new V(v.X,-.45f+v.Y*.94f-v.Z*.342f,-.39f+v.Y*(-.342f)+v.Z*.94f);}m.Add(face);}
   var baseControls=new List<Face>();Screen(baseControls,0,0,0,1.02f,.76f);Pad(baseControls,-.78f,-.13f,.025f);Disc(baseControls,silver,-.78f,.28f,.025f,.105f,.035f,true);Buttons(baseControls,.79f,.13f,.025f);
   foreach(Face face in baseControls){for(int i=0;i<face.Vertices.Length;i++){V v=face.Vertices[i];face.Vertices[i]=new V(v.X,-.411f+v.Z,.2f-v.Y);}m.Add(face);}
  }else if(key=="Nintendo 64"){
   Box(m,dark,0,-.23f,0,2.2f,.48f,1.55f);Box(m,dark,0,.06f,-.12f,1.8f,.2f,1.16f);Box(m,black,0,.178f,-.27f,1.08f,.026f,.15f);
   Box(m,Color.FromArgb(90,97,105),0,.38f,-.27f,1.04f,.42f,.12f);
   for(int i=0;i<4;i++)Disc(m,black,-.77f+i*.51f,-.24f,.79f,.12f,.03f,true);
   Box(m,silver,-.85f,.032f,.51f,.23f,.065f,.25f);Box(m,silver,.85f,.032f,.51f,.23f,.065f,.25f);
  }else if(key=="Switch"||key=="Wii U"){
   Box(m,dark,0,0,0,2.1f,1.14f,.18f);Screen(m,0,0,.101f,1.87f,.99f);
   Color left=key=="Switch"?Color.FromArgb(39,186,213):dark,right=key=="Switch"?Color.FromArgb(234,80,84):dark;
   Box(m,left,-1.2f,0,0,.38f,1.14f,.23f);Box(m,right,1.2f,0,0,.38f,1.14f,.23f);
   Disc(m,black,-1.2f,.26f,.14f,.11f,.06f,true);Pad(m,-1.2f,-.22f,.14f);Buttons(m,1.2f,.22f,.14f);Disc(m,black,1.2f,-.27f,.14f,.11f,.06f,true);
   if(key=="Wii U")Box(m,dark,0,-.71f,-.48f,2.05f,.25f,1.3f);
  }else if(key=="Wii"){
   Box(m,silver,0,-.83f,0,1.05f,.14f,1.1f);Box(m,Color.FromArgb(232,238,240),0,0,0,.55f,1.7f,1.16f);
   Box(m,blue,.09f,.13f,.589f,.047f,1.12f,.02f);Box(m,black,.09f,.13f,.603f,.015f,1.04f,.01f);
   Disc(m,silver,-.14f,.65f,.59f,.036f,.02f,true);Disc(m,Color.FromArgb(105,226,196),-.14f,.48f,.59f,.026f,.02f,true);
  }else if(key=="Dreamcast"){
   Box(m,silver,0,-.18f,0,1.98f,.49f,1.76f);Disc(m,Color.FromArgb(222,230,234),0,.09f,-.12f,.74f,.08f,false);
   Disc(m,Color.FromArgb(232,130,67),0,.14f,-.12f,.15f,.018f,false);
   for(int i=0;i<4;i++)Box(m,black,-.7f+i*.46f,-.18f,.893f,.29f,.18f,.025f);
   Disc(m,dark,-.77f,.08f,.59f,.1f,.04f,false);Disc(m,dark,.77f,.08f,.59f,.1f,.04f,false);
  }else if(key=="PS2"){
   Box(m,dark,0,0,0,.57f,1.88f,1.35f);Box(m,black,.06f,0,.05f,.62f,1.63f,1.36f);Box(m,blue,0,-.99f,0,1.04f,.12f,1.48f);
   for(int i=0;i<9;i++)Box(m,Color.FromArgb(68,76,88),-.285f,-.7f+i*.18f,.714f,.08f,.028f,.024f);
   Box(m,silver,.12f,.44f,.74f,.07f,.65f,.02f);Box(m,blue,.12f,-.14f,.74f,.035f,.21f,.02f);
  }else if(key=="PS3"){
   Box(m,black,0,0,0,1.18f,1.95f,.82f);Box(m,dark,0,0,.1f,1.3f,1.78f,.86f);Box(m,silver,0,-.67f,.54f,1.16f,.055f,.025f);Box(m,black,0,-.49f,.542f,.94f,.04f,.02f);
   for(int i=0;i<6;i++)Box(m,silver,-.45f+i*.18f,.76f,.535f,.05f,.1f,.018f);
  }else{
   Box(m,dark,0,0,0,1.12f,1.85f,1.22f);Box(m,black,0,0,.62f,.97f,1.65f,.03f);
   Disc(m,blue,0,.32f,.655f,.31f,.027f,true);Disc(m,black,0,.32f,.68f,.23f,.027f,true);Disc(m,blue,0,-.4f,.655f,.31f,.027f,true);Disc(m,black,0,-.4f,.68f,.23f,.027f,true);
   Disc(m,silver,.34f,.73f,.65f,.04f,.03f,true);
  }
  cache[key]=m;return m;
 }
 static V Rotate(V p,double angle){float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle),x=p.X*c+p.Z*s,z=-p.X*s+p.Z*c;return new V(x,p.Y*.94f-z*.342f,p.Y*.342f+z*.94f);}
 public static Bitmap Rasterize(List<Polygon> polygons,out Point location){
  float left=1440,top=940,right=0,bottom=0;
  foreach(var p in polygons)foreach(var v in p.Points){left=Math.Min(left,v.X);top=Math.Min(top,v.Y);right=Math.Max(right,v.X);bottom=Math.Max(bottom,v.Y);}
  location=new Point((int)Math.Floor(left)-1,(int)Math.Floor(top)-1);int width=Math.Max(1,(int)Math.Ceiling(right)-location.X+2),height=Math.Max(1,(int)Math.Ceiling(bottom)-location.Y+2);
  var pixels=new int[width*height];var depths=new float[pixels.Length];for(int i=0;i<depths.Length;i++)depths[i]=float.NegativeInfinity;
  foreach(var p in polygons)for(int triangle=1;triangle<p.Points.Length-1;triangle++){
   PointF a=p.Points[0],b=p.Points[triangle],c=p.Points[triangle+1];
   a.X-=location.X;a.Y-=location.Y;b.X-=location.X;b.Y-=location.Y;c.X-=location.X;c.Y-=location.Y;
   float det=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);if(Math.Abs(det)<.00001f)continue;
   int x0=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X)))),x1=Math.Min(width-1,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
   int y0=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y)))),y1=Math.Min(height-1,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
   for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
    float u=((b.Y-c.Y)*(x+.5f-c.X)+(c.X-b.X)*(y+.5f-c.Y))/det,v=((c.Y-a.Y)*(x+.5f-c.X)+(a.X-c.X)*(y+.5f-c.Y))/det,w=1-u-v;
    if(u<-.00001f||v<-.00001f||w<-.00001f)continue;
    float depth=u*p.Depths[0]+v*p.Depths[triangle]+w*p.Depths[triangle+1];int index=y*width+x;
    if(depth>depths[index]){depths[index]=depth;pixels[index]=p.Color.ToArgb();}
   }
  }
  var bitmap=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var data=bitmap.LockBits(new Rectangle(0,0,width,height),System.Drawing.Imaging.ImageLockMode.WriteOnly,bitmap.PixelFormat);
  try{System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{bitmap.UnlockBits(data);}return bitmap;
 }
 public static List<Polygon> Project(string platform,double angle,float eye,float aspect,bool software=false,float baseDepth=-520,float centerY=556){
  var result=new List<Polygon>();foreach(Face f in Mesh(platform)){
   V[] v=new V[f.Vertices.Length];for(int i=0;i<v.Length;i++)v[i]=Rotate(f.Vertices[i],angle);
   float ax=v[1].X-v[0].X,ay=v[1].Y-v[0].Y,az=v[1].Z-v[0].Z,bx=v[2].X-v[0].X,by=v[2].Y-v[0].Y,bz=v[2].Z-v[0].Z;
   float nx=ay*bz-az*by,ny=az*bx-ax*bz,nz=ax*by-ay*bx,len=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);
   if(len<.00001f)continue;
   // Cull in camera space, allowing the small eye offset used by stereo projection.
   if(nx*(eye*aspect-v[0].X*250)+ny*(-v[0].Y*250)+nz*(900-baseDepth-v[0].Z*250)<=0)continue;
   float light=.57f+.43f*Math.Max(0,(-nx*.4f+ny*.7f+nz*.6f)/len);
   Color color=Color.FromArgb((int)(f.Color.R*light),(int)(f.Color.G*light),(int)(f.Color.B*light));
   var points=new PointF[v.Length];var depths=new float[v.Length];float depth=0;
   for(int i=0;i<v.Length;i++){float z=baseDepth+v[i].Z*250,k=900/(900-z);points[i]=new PointF(720+v[i].X*250*k/aspect+StereoMath.Parallax(eye,z),centerY-v[i].Y*250*k);depths[i]=k-1;depth+=z;}
   result.Add(new Polygon{Points=points,Depths=depths,Color=color,Depth=depth/v.Length});
  }
  result.Sort((a,b)=>a.Depth.CompareTo(b.Depth));return result;
 }
}

public partial class DepthWindow {
 double lastConsoleFrame;
 string ConsolePlatform {get{return filter=="Library"||filter=="Favorites"?(FocusedCover==null?null:FocusedCover.Platform):filter;}}
 bool ConsoleVisible {get{return panel==""&&!libraryMasked&&activeGame==null&&ConsolePlatform!=null;}}
 void AnimateConsole(double now){if(ConsoleVisible&&(gpu!=null||now-lastConsoleFrame>=1.0/24)){lastConsoleFrame=now;InvalidateLogical(new RectangleF(380,295,680,535));}}
 void DrawConsole(Graphics g,int eye,int count,float width){
  if(!ConsoleVisible)return;float entrance=revealSince<0?1:ArrivalProgress(ArrivalTime,1);float baseDepth=-520+920*(1-entrance),centerY=556-2200*(1-entrance);
  float aspect=ClientSize.Width*940f/(Math.Max(1,ClientSize.Height)*1440f);
  if(g==null&&gpu.ImportedConsole(ConsolePlatform,frameClock.Elapsed.TotalSeconds*Math.PI/24+.45,Eye(eye,count),aspect,(int)(eye*width),(int)width,ClientSize.Height,baseDepth,centerY)){gpu.Begin(ClientSize.Width,ClientSize.Height);return;}
  var polygons=ConsoleModels.Project(ConsolePlatform,frameClock.Elapsed.TotalSeconds*Math.PI/24+.45,Eye(eye,count),aspect,g!=null,baseDepth,centerY);
  if(g==null){gpu.BeginConsole();foreach(var p in polygons){PointF[] points=new PointF[p.Points.Length];for(int i=0;i<points.Length;i++)points[i]=new PointF(eye*width+p.Points[i].X*width/1440,p.Points[i].Y*ClientSize.Height/940);gpu.ConsolePolygon(points,p.Depths,p.Color);}gpu.EndConsole();}
  else {Point location;using(var bitmap=ConsoleModels.Rasterize(polygons,out location))g.DrawImage(bitmap,location.X,location.Y,bitmap.Width,bitmap.Height);}
 }
}

public sealed partial class GpuRenderer {
 [System.Runtime.InteropServices.DllImport("opengl32.dll")] static extern void glVertex3f(float x,float y,float z);
 public void BeginConsole(){glClear(0x0100);glEnable(0x0B71);}
 public void EndConsole(){glDisable(0x0B71);}
 public void ConsolePolygon(PointF[] points,float[] depths,Color color){glDisable(0x0DE1);glColor4f(color.R/255f,color.G/255f,color.B/255f,1);glBegin(6);for(int i=0;i<points.Length;i++)glVertex3f(points[i].X,points[i].Y,depths[i]);glEnd();}
}
