using System;
using System.Drawing;
using System.Drawing.Drawing2D;

// Procedural closed game-case mesh. Front, back, spine and rim are projected
// from the same rotated cuboid in both rendering paths and in both stereo eyes.
public partial class DepthWindow {
 bool compareLegacyCases=false;
 float NeighborLift(Game game){float value;return !ScrollAnimating&&panel==""&&game!=null&&caseLift.TryGetValue(game,out value)?value:0;}
 readonly System.Collections.Generic.Dictionary<Game,float> caseLift=new System.Collections.Generic.Dictionary<Game,float>();
 sealed class CaseWave {public PointF Center;public float Age;}
 readonly System.Collections.Generic.List<CaseWave> caseWaves=new System.Collections.Generic.List<CaseWave>();
 readonly System.Collections.Generic.Dictionary<Game,float> caseVelocity=new System.Collections.Generic.Dictionary<Game,float>();
 readonly System.Collections.Generic.Dictionary<Game,float> caseBreath=new System.Collections.Generic.Dictionary<Game,float>();
 readonly System.Collections.Generic.Dictionary<Game,float> caseTurnLift=new System.Collections.Generic.Dictionary<Game,float>();
 readonly System.Collections.Generic.Dictionary<Game,PointF> caseSway=new System.Collections.Generic.Dictionary<Game,PointF>();
 readonly System.Collections.Generic.Dictionary<Game,PointF> casePointerAim=new System.Collections.Generic.Dictionary<Game,PointF>();
 static PointF CasePointerResponse(float dx,float dy){
  float distance=(float)Math.Sqrt(dx*dx+dy*dy);const float calm=.32f;
  if(distance<=calm)return PointF.Empty;
  float t=Math.Min(1,(distance-calm)/(1-calm));t=t*t*(3-2*t);
  return new PointF(dx/distance*t*.16f,-dy/distance*t*.12f);
 }
 PointF CasePointerTarget(Game game,int slot){
  RectangleF bounds=CardBounds(slot,Hover(game),headX);if(!bounds.Contains(pointer))return PointF.Empty;
  float depth=bounds.Height*.04f,k=900/(900-depth);
  PointF center=ProjectCasePoint(bounds,0,0,0,0,depth,0);
  return CasePointerResponse((pointer.X-center.X)/(bounds.Width*k/2),(pointer.Y-center.Y)/(bounds.Height*k/2));
 }
 bool InspectingArtwork {
  get{if(panel!=""||ScrollAnimating||platformFocused||!pointerInside||over==null)return false;
   int slot=shown.IndexOf(over)-page*PageSize;return slot>=0&&slot<PageSize&&CardBounds(slot,Hover(over),headX).Contains(pointer);
  }
 }
 void AnimateCasePointerAim(Game game,Game focus,int slot,float dt){
  PointF oldAim;casePointerAim.TryGetValue(game,out oldAim);
  PointF wanted=game==focus&&over==game&&pointerInside?CasePointerTarget(game,slot):PointF.Empty;
  float blend=CaseBlend(dt,9);PointF aim=new PointF(oldAim.X+(wanted.X-oldAim.X)*blend,oldAim.Y+(wanted.Y-oldAim.Y)*blend);
  if(Math.Abs(aim.X-wanted.X)<.00005f)aim.X=wanted.X;if(Math.Abs(aim.Y-wanted.Y)<.00005f)aim.Y=wanted.Y;
  casePointerAim[game]=aim;if(oldAim!=aim)InvalidateCard(game);
 }
 double caseBreathTime;
 PointF CaseSurfaceAngles(Game game,float h){
  float turnLift;caseTurnLift.TryGetValue(game,out turnLift);PointF sway;caseSway.TryGetValue(game,out sway);
  PointF aim;casePointerAim.TryGetValue(game,out aim);
  if(ScrollAnimating||panel!=""){turnLift=0;sway=PointF.Empty;aim=PointF.Empty;}
  float turn=CaseRotation(h)*(1-Math.Min(.62f,turnLift*2)*(1-h));
  return new PointF(-.32f*turn+sway.X*(1-h)+aim.X*h,.075f*turn+sway.Y*(1-h)+aim.Y*h);
 }
 Game waveFocus;PointF lastWaveCenter;float waveElapsed=1;string waveGrid="";
 static float CaseBlend(float dt,float speed){return (float)(1-Math.Exp(-Math.Min(.05f,Math.Max(0,dt))*speed));}
 static float CaseRotation(float hover){return 1-hover;}
 static float CaseRipple(float distance,float age){
  if(age<0||age>1.1f)return 0;
  float delta=(distance-age*5f)/.62f;
  return .24f*(float)Math.Exp(-delta*delta-age*2.8f);
 }
 void AnimateCases(float dt){
  dt=Math.Min(.05f,Math.Max(0,dt));waveElapsed+=dt;
  string grid=page+"|"+filter+"|"+GridColumns;if(grid!=waveGrid){waveGrid=grid;caseWaves.Clear();caseLift.Clear();caseVelocity.Clear();caseBreath.Clear();caseTurnLift.Clear();caseSway.Clear();casePointerAim.Clear();waveFocus=null;}

  Game focus=panel==""&&!ScrollAnimating&&!platformFocused?FocusedCover:null;
  int focused=focus==null?-1:shown.IndexOf(focus)-page*PageSize;
  bool inspecting=InspectingArtwork;
  if(inspecting)caseWaves.Clear();else {caseBreathTime+=dt;
  for(int i=caseWaves.Count-1;i>=0;i--){caseWaves[i].Age+=dt;if(caseWaves[i].Age>1.1f)caseWaves.RemoveAt(i);}
  }
  PointF center=PointF.Empty;
  if(focused>=0&&focused<PageSize){
   center=new PointF(focused%GridColumns,focused/GridColumns);
   if(over==focus&&pointerInside){RectangleF bounds=CardBounds(focused,Hover(focus),headX);
    center.X+=Math.Max(-1,Math.Min(1,(pointer.X-bounds.X-bounds.Width/2)/(bounds.Width/2)))*.28f;
    center.Y+=Math.Max(-1,Math.Min(1,(pointer.Y-bounds.Y-bounds.Height/2)/(bounds.Height/2)))*.28f;
   }
   float dx=center.X-lastWaveCenter.X,dy=center.Y-lastWaveCenter.Y;
   if(!inspecting&&(focus!=waveFocus||(waveElapsed>=.10f&&dx*dx+dy*dy>.065f))){
    if(caseWaves.Count>=6)caseWaves.RemoveAt(0);
    caseWaves.Add(new CaseWave{Center=center});lastWaveCenter=center;waveElapsed=0;
   }
  }
  waveFocus=focus;
  if(ScrollAnimating||panel!=""){caseWaves.Clear();}
  for(int i=page*PageSize;i<Math.Min(shown.Count,page*PageSize+PageSize);i++){
   Game game=shown[i];int slot=i-page*PageSize;AnimateCasePointerAim(game,focus,slot,dt);
   // Freeze the surrounding arrangement while artwork is being inspected.
   // Its motion clock is paused too, so resuming cannot jump to a new phase.
   if(inspecting)continue;float target=0;
   if(focused>=0&&focused<PageSize&&game!=focus){float dx=center.X-slot%GridColumns,dy=center.Y-slot/GridColumns;target=.22f*(float)Math.Exp(-(dx*dx+dy*dy)/2.3f)*Hover(focus);}
   if(game!=focus)foreach(var wave in caseWaves){float dx=wave.Center.X-slot%GridColumns,dy=wave.Center.Y-slot/GridColumns;target+=CaseRipple((float)Math.Sqrt(dx*dx+dy*dy),wave.Age);}
   target=Math.Min(.38f,target);
   // Critically damped motion retains a soft wake while the mouse moves,
   // without bouncing or snapping when the highlighted title changes.
   float previous,velocity;caseLift.TryGetValue(game,out previous);caseVelocity.TryGetValue(game,out velocity);
   const float speed=12;float decay=(float)Math.Exp(-speed*dt),offset=previous-target,temp=(velocity+speed*offset)*dt;
   float next=target+(offset+temp)*decay;velocity=(velocity-speed*temp)*decay;
   if(Math.Abs(next-target)<.0003f&&Math.Abs(velocity)<.003f){next=target;velocity=0;}
   caseLift[game]=Math.Max(0,Math.Min(.38f,next));caseVelocity[game]=velocity;
   // Rotation settles more slowly than the forward lift. A very small,
   // two-axis sway continues through the settling tail, with staggered phases
   // so the cases breathe rather than turning in lockstep.
   float oldTurn;caseTurnLift.TryGetValue(game,out oldTurn);
   float turnLift=oldTurn+(caseLift[game]-oldTurn)*CaseBlend(dt,2.8f);
   if(Math.Abs(turnLift-caseLift[game])<.0001f)turnLift=caseLift[game];caseTurnLift[game]=turnLift;
   float breath;caseBreath.TryGetValue(game,out breath);
   breath=Math.Min(1,Math.Max(caseLift[game]*1.8f,breath*(float)Math.Exp(-dt/2.0f)+Math.Abs(caseLift[game]-previous)*12));
   if(breath<.0007f)breath=0;caseBreath[game]=breath;
   PointF oldSway;caseSway.TryGetValue(game,out oldSway);
   double phase=slot*.79+Math.Sin(slot*1.7)*.3;
   PointF sway=new PointF(.022f*breath*(float)Math.Sin(caseBreathTime*1.1+phase),.012f*breath*(float)Math.Sin(caseBreathTime*.83+phase*1.4));
   caseSway[game]=sway;
   if(previous!=caseLift[game]||oldTurn!=turnLift||oldSway!=sway)InvalidateCard(game);
  }
 }

 static float CaseRatio(Game game,float fallback){
  string p=game==null?"":game.Platform.ToLowerInvariant();
  if(p.Contains("switch"))return .615f;
  if(p.Contains("dreamcast")||p.Contains("3ds")||p=="ds"||p.Contains("playstation 1")||p=="ps1")return 1.12f;
  if(p.Contains("psp"))return .59f;
  if(p=="ps3"||p=="ps4"||p=="ps5")return .79f;
  if(p.Contains("wii")||p.Contains("gamecube")||p=="ps2"||p.Contains("xbox"))return .71f;
  if(p=="steam"||p=="pc")return .667f;
  return fallback;
 }
 static Color CasePlastic(Game game){
  string p=game.Platform.ToLowerInvariant();
  if(p.Contains("switch"))return Color.FromArgb(180,32,41);
  if(p.Contains("wii")||p.Contains("3ds")||p=="ds")return Color.FromArgb(216,222,224);
  if(p=="ps4"||p=="ps5"||p.Contains("wii u"))return Color.FromArgb(23,85,166);
  if(p.Contains("dreamcast")||p=="ps1")return Color.FromArgb(157,169,181);
  if(p.Contains("xbox"))return Color.FromArgb(55,124,40);
  return Color.FromArgb(45,49,56);
 }
 static Color CaseShade(Color color,float light,int alpha){return Color.FromArgb(alpha,Math.Min(255,(int)(color.R*light)),Math.Min(255,(int)(color.G*light)),Math.Min(255,(int)(color.B*light)));}
 PointF CasePoint(RectangleF r,float h,float x,float y,float z,float eye){return ProjectCasePoint(r,-.32f*CaseRotation(h),.075f*CaseRotation(h),x,y,z,eye);}
 PointF ProjectCasePoint(RectangleF r,float yaw,float pitch,float x,float y,float z,float eye){
  float xx=x*(float)Math.Cos(yaw)+z*(float)Math.Sin(yaw),zz=-x*(float)Math.Sin(yaw)+z*(float)Math.Cos(yaw);
  float yy=y*(float)Math.Cos(pitch)-zz*(float)Math.Sin(pitch);zz=y*(float)Math.Sin(pitch)+zz*(float)Math.Cos(pitch);
  float k=900/(900-zz);
  // Every case shares the same vertical camera and horizon. Projecting around
  // each individual card center made the two rows imply different viewpoints.
  float cameraY=470+headY,centerY=r.Y+r.Height/2;
  return new PointF(r.X+r.Width/2+(xx-eye*zz/900)*k,cameraY+(centerY-cameraY+yy)*k);
 }
 static PointF[] CaseContour(float x,float y,float radius){
  var ring=new PointF[32];int n=0;
  for(int corner=0;corner<4;corner++)for(int step=0;step<8;step++){
   double angle=(-180+corner*90+step*90/7.0)*Math.PI/180;
   float cx=corner==0||corner==3?-x+radius:x-radius,cy=corner<2?-y+radius:y-radius;
   ring[n++]=new PointF(cx+radius*(float)Math.Cos(angle),cy+radius*(float)Math.Sin(angle));
  }return ring;
 }
 static float CaseTop(float x,float halfWidth,float halfHeight,float radius){
  float edge=Math.Max(0,Math.Abs(x)-(halfWidth-radius));return -halfHeight+radius-(float)Math.Sqrt(Math.Max(0,radius*radius-edge*edge));
 }
 static float[] CaseArtCuts(float halfWidth,float radius){
  // Follow the circular corners in angle space. Uniform vertical strips left
  // long diagonal cuts where the curve becomes almost vertical.
  var cuts=new System.Collections.Generic.SortedSet<float>();
  for(int i=0;i<=24;i++)cuts.Add(i/24f);
  for(int i=0;i<=16;i++){float u=radius*(1-(float)Math.Cos(i*Math.PI/32))/(2*halfWidth);cuts.Add(u);cuts.Add(1-u);}
  var result=new float[cuts.Count];cuts.CopyTo(result);return result;
 }
 void CaseArtTriangle(Graphics g,Bitmap image,PointF[] points,PointF[] uv,int a,int b,int c,Color tint){
  float ax=uv[a].X*image.Width,ay=uv[a].Y*image.Height,bx=uv[b].X*image.Width-ax,by=uv[b].Y*image.Height-ay,cx=uv[c].X*image.Width-ax,cy=uv[c].Y*image.Height-ay;
  float det=bx*cy-cx*by;if(Math.Abs(det)<.00001f)return;
  float dx=points[b].X-points[a].X,dy=points[b].Y-points[a].Y,ex=points[c].X-points[a].X,ey=points[c].Y-points[a].Y;
  float m11=(dx*cy-ex*by)/det,m21=(ex*bx-dx*cx)/det,m12=(dy*cy-ey*by)/det,m22=(ey*bx-dy*cx)/det;
  var state=g.Save();
  try{using(var clip=new GraphicsPath())using(var matrix=new Matrix(m11,m12,m21,m22,points[a].X-m11*ax-m21*ay,points[a].Y-m12*ax-m22*ay))using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
   clip.AddPolygon(new[]{points[a],points[b],points[c]});g.SetClip(clip,CombineMode.Intersect);
   var dest=new[]{new PointF(0,0),new PointF(image.Width,0),new PointF(0,image.Height)};matrix.TransformPoints(dest);
   var colors=new System.Drawing.Imaging.ColorMatrix();colors.Matrix00=tint.R/255f;colors.Matrix11=tint.G/255f;colors.Matrix22=tint.B/255f;colors.Matrix33=tint.A/255f;attributes.SetColorMatrix(colors);attributes.SetWrapMode(WrapMode.TileFlipXY);
   g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.Half;
   g.DrawImage(image,dest,new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel,attributes);
  }}finally{g.Restore(state);}
 }
 Bitmap caseReflection;
 static PointF CaseReflectionUv(PointF uv,float yaw,float pitch,float eye){
  // A stationary area light reflected by the surface: rotating the case moves
  // the reflected light across the insert, rather than painting a gradient.
  return new PointF(uv.X+yaw*.85f+pitch*.20f+eye*.0006f,uv.Y+pitch*1.10f-yaw*.12f);
 }
 static float ReflectionFade(float value){value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
 Bitmap CaseReflection(){
  if(caseReflection!=null)return caseReflection;const int size=256;var bytes=new byte[size*size*4];
  for(int y=0;y<size;y++)for(int x=0;x<size;x++){
   float u=(x+.5f)/size,v=(y+.5f)/size,dx=u-.5f,dy=v-.5f;
   float across=dx*.77f-dy*.64f,along=dx*.64f+dy*.77f;
   // A finite, diagonally oriented softbox, with rounded ends and a broad
   // penumbra. The center is gently even; the reflection fades on every side.
   float qx=Math.Abs(across)-.105f+.045f,qy=Math.Abs(along)-.47f+.045f;
   float distance=(float)Math.Sqrt(Math.Max(0,qx)*Math.Max(0,qx)+Math.Max(0,qy)*Math.Max(0,qy))+Math.Min(Math.Max(qx,qy),0)-.045f;
   float edge=ReflectionFade(Math.Min(Math.Min(u,1-u),Math.Min(v,1-v))/.085f);
   byte a=(byte)Math.Round(76*ReflectionFade(.5f-distance/.14f)*edge);
   int at=(y*size+x)*4;bytes[at]=(byte)(a*249/255);bytes[at+1]=(byte)(a*253/255);bytes[at+2]=a;bytes[at+3]=a;
  }
  caseReflection=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
  var data=caseReflection.LockBits(new Rectangle(0,0,size,size),System.Drawing.Imaging.ImageLockMode.WriteOnly,caseReflection.PixelFormat);
  try{System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{caseReflection.UnlockBits(data);}return caseReflection;
 }
 void DisposeCaseReflection(){if(caseReflection!=null){if(gpu!=null)gpu.Forget(caseReflection);caseReflection.Dispose();caseReflection=null;}}
 void DrawGameCase(Graphics g,Game game,RectangleF r,float h,float eye,int eyeIndex,float screenWidth,float alpha){
  if(gpu!=null&&!compareLegacyCases){DrawFastGameCase(game,r,h,eye,eyeIndex,screenWidth,alpha);return;}
  float x=r.Width/2,y=r.Height/2,d=r.Height*.040f,radius=Math.Min(r.Width,r.Height)*.065f;
  PointF angles=CaseSurfaceAngles(game,h);
  float yaw=angles.X,pitch=angles.Y;
  Color plastic=CasePlastic(game);int opacity=(int)(255*alpha);
  PointF[] ring=CaseContour(x,y,radius),back=new PointF[ring.Length],front=new PointF[ring.Length];
  for(int i=0;i<ring.Length;i++){front[i]=ProjectCasePoint(r,yaw,pitch,ring[i].X,ring[i].Y,d,eye);back[i]=ProjectCasePoint(r,yaw,pitch,ring[i].X,ring[i].Y,-d,eye);}
  CaseFace(g,null,back,RectangleF.Empty,CaseShade(plastic,.6f,opacity),eyeIndex,screenWidth);
  for(int i=0;i<ring.Length;i++){
   int j=(i+1)%ring.Length;float ex=ring[j].X-ring[i].X,ey=ring[j].Y-ring[i].Y,length=(float)Math.Sqrt(ex*ex+ey*ey);
   float light=.78f+(length<.001f?0:(ex*.38f+ey*.18f)/length);
   CaseFace(g,null,new[]{back[i],back[j],front[j],front[i]},RectangleF.Empty,CaseShade(plastic,light,opacity),eyeIndex,screenWidth);
  }
  CaseFace(g,null,front,RectangleF.Empty,CaseShade(plastic,1.12f,opacity),eyeIndex,screenWidth);
  float inset=Math.Max(2,r.Width*.019f),ix=x-inset,iy=y-inset,ir=Math.Max(1,radius-inset);
  Bitmap image=Sprite(game),reflection=CaseReflection();GraphicsState saved=null;
  if(gpu==null){saved=g.Save();using(var clip=new GraphicsPath()){clip.AddPolygon(front);g.SetClip(clip,CombineMode.Intersect);}}
  float[] cuts=CaseArtCuts(ix,ir);
  for(int i=0;i<cuts.Length-1;i++){
   float u=cuts[i],v=cuts[i+1];if(gpu==null){u=Math.Max(0,u-.001f);v=Math.Min(1,v+.001f);}
   float a=-ix+2*ix*u,b=-ix+2*ix*v,ta=CaseTop(a,ix,iy,ir),tb=CaseTop(b,ix,iy,ir);
   PointF[] quad={ProjectCasePoint(r,yaw,pitch,a,ta,d+.12f,eye),ProjectCasePoint(r,yaw,pitch,b,tb,d+.12f,eye),ProjectCasePoint(r,yaw,pitch,b,-tb,d+.12f,eye),ProjectCasePoint(r,yaw,pitch,a,-ta,d+.12f,eye)};
   PointF[] uv={new PointF(u,(ta+iy)/(2*iy)),new PointF(v,(tb+iy)/(2*iy)),new PointF(v,(-tb+iy)/(2*iy)),new PointF(u,(-ta+iy)/(2*iy))};
   Color tint=CaseShade(Color.White,.94f+.06f*h,opacity);
   if(gpu!=null){var pixels=new PointF[4];for(int n=0;n<4;n++)pixels[n]=CasePixels(quad[n],eyeIndex,screenWidth);gpu.CasePatch(image,pixels,uv,tint);}
   else {CaseArtTriangle(g,image,quad,uv,0,1,2,tint);CaseArtTriangle(g,image,quad,uv,0,2,3,tint);}
   var lightUv=new PointF[4];for(int n=0;n<4;n++)lightUv[n]=CaseReflectionUv(uv[n],yaw,pitch,eye);
   float reflectance=.75f+.25f*(1-(float)Math.Cos(yaw)*(float)Math.Cos(pitch));
   Color reflected=Color.FromArgb((int)(255*alpha*reflectance),255,255,255);
   if(gpu!=null){var pixels=new PointF[4];for(int n=0;n<4;n++)pixels[n]=CasePixels(quad[n],eyeIndex,screenWidth);gpu.CasePatch(reflection,pixels,lightUv,reflected);}
   else {CaseArtTriangle(g,reflection,quad,lightUv,0,1,2,reflected);CaseArtTriangle(g,reflection,quad,lightUv,0,2,3,reflected);}
  }
  if(saved!=null)g.Restore(saved);
  if(gpu!=null){var points=new PointF[front.Length];for(int i=0;i<front.Length;i++)points[i]=CasePixels(front[i],eyeIndex,screenWidth);gpu.CaseEdge(points,Color.FromArgb((int)(150*alpha),185,203,213));}
  else using(var pen=new Pen(Color.FromArgb((int)(150*alpha),200,218,230),.7f))g.DrawPolygon(pen,front);
 }
 PointF CasePixels(PointF p,int eye,float width){return new PointF(eye*width+p.X*width/1440,p.Y*ClientSize.Height/940);}
 void CaseFace(Graphics g,Bitmap image,PointF[] points,RectangleF uv,Color color,int eye,float width){
  if(gpu!=null){var pixels=new PointF[points.Length];for(int i=0;i<points.Length;i++)pixels[i]=CasePixels(points[i],eye,width);gpu.CaseQuad(image,pixels,uv,color);return;}
  if(image==null){using(var brush=new SolidBrush(color))g.FillPolygon(brush,points);return;}
  using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
   attributes.SetWrapMode(WrapMode.TileFlipXY);g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.Half;
   var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix00=color.R/255f;matrix.Matrix11=color.G/255f;matrix.Matrix22=color.B/255f;matrix.Matrix33=color.A/255f;attributes.SetColorMatrix(matrix);
   g.DrawImage(image,new[]{points[0],points[1],points[3]},new RectangleF(uv.X*image.Width,uv.Y*image.Height,uv.Width*image.Width,uv.Height*image.Height),GraphicsUnit.Pixel,attributes);
  }
 }
}
public sealed partial class GpuRenderer {
 public void CaseQuad(Bitmap image,PointF[] p,RectangleF uv,Color color){
  if(image!=null){glEnable(0x0DE1);glBindTexture(0x0DE1,Texture(image));}else glDisable(0x0DE1);
  float alpha=color.A/255f;glColor4f(color.R/255f*alpha,color.G/255f*alpha,color.B/255f*alpha,alpha);
  glBegin(image==null?9u:7u);
  for(int i=0;i<p.Length;i++){if(image!=null)glTexCoord2f(i==0||i==3?uv.Left:uv.Right,i<2?uv.Top:uv.Bottom);glVertex2f(p[i].X,p[i].Y);}glEnd();
 }
 public void CasePatch(Bitmap image,PointF[] p,PointF[] uv,Color c){
  glEnable(0x0DE1);glBindTexture(0x0DE1,Texture(image));float a=c.A/255f;glColor4f(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);glBegin(7);
  for(int i=0;i<4;i++){glTexCoord2f(uv[i].X,uv[i].Y);glVertex2f(p[i].X,p[i].Y);}glEnd();
 }
 public void CaseEdge(PointF[] p,Color c){glDisable(0x0DE1);float a=c.A/255f;glColor4f(c.R/255f*a,c.G/255f*a,c.B/255f*a,a);glLineWidth(1);glBegin(2);foreach(var v in p)glVertex2f(v.X,v.Y);glEnd();}
}



















