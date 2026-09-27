using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;

public partial class DepthWindow {
 const int IntroSettle=850,IntroHold=400,IntroFlight=3600,IntroDuration=IntroHold+IntroFlight;
 bool startupOpening;
 long introSince=-1,lastUiSound=-1000;
 GraphicsPath openingTitle;Bitmap openingBackground;long revealSince=-1;
 bool openingAssetsPrepared;
 void PrepareOpeningAssets(){
  if(gpu==null||openingAssetsPrepared)return;
  var watch=System.Diagnostics.Stopwatch.StartNew();
  PrepareChrome(lib.Stereo?2:1,ClientSize.Width/(lib.Stereo?2f:1f));gpu.PreloadImage(chrome);
  PrepareArrival();foreach(var layer in arrivalLayers)gpu.PreloadImage(layer);
  for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++)gpu.PreloadImage(Sprite(shown[i]));
  // Upload first-page titles before navigation begins; retain the real selection.
  var savedSelected=selected;var savedOver=over;
  try{for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++){selected=shown[i];over=null;PrepareArrival();gpu.PreloadImage(arrivalLayers[1]);gpu.PreloadImage(arrivalLayers[3]);}}
  finally{selected=savedSelected;over=savedOver;PrepareArrival();}
  var systems=new System.Collections.Generic.HashSet<string>();if(ConsolePlatform!=null)systems.Add(ConsolePlatform);
  for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++)systems.Add(shown[i].Platform);
  foreach(string system in systems)gpu.ImportedConsole(system,.45,0,ClientSize.Width*940f/(ClientSize.Height*1440f),0,ClientSize.Width,ClientSize.Height);
  // Exercise shader/texture uploads in the back buffer, then present black only.
  gpu.Begin(ClientSize.Width,ClientSize.Height);gpu.Black();gpu.Finish(true);openingAssetsPrepared=true;
  watch.Stop();try{File.WriteAllText(Path.Combine(Storage.Folder,"opening-preload.txt"),"Prepared visible covers, interface and "+ConsolePlatform+" model behind black in "+watch.Elapsed.TotalMilliseconds.ToString("F1")+" ms.");}catch{}
 }
 SoundPlayer moveSound,selectSound;
 MemoryStream moveWave,selectWave;
 static GraphicsPath Rounded(RectangleF r,float radius){
  var p=new GraphicsPath();float d=radius*2;
  p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
  p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
 }
 static MemoryStream SoftTone(bool select){
  const int rate=22050;int n=(int)(rate*(select?.105:.045));var stream=new MemoryStream();
  var writer=new BinaryWriter(stream);writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+n*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
  writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(n*2);
  for(int i=0;i<n;i++){double t=i/(double)rate,envelope=Math.Pow(Math.Sin(Math.PI*i/(n-1)),2)*Math.Exp(-3.0*i/n);double wave=Math.Sin(2*Math.PI*(select?660:480)*t);if(select)wave=.7*wave+.3*Math.Sin(2*Math.PI*880*t);writer.Write((short)(wave*envelope*3300));}
  writer.Flush();stream.Position=0;return stream;
 }
 void UiSound(bool select){
  if(libraryMasked||introSince>=0||!Visible||frameClock.ElapsedMilliseconds-lastUiSound<65)return;
  lastUiSound=frameClock.ElapsedMilliseconds;
  try{if(moveSound==null){moveWave=SoftTone(false);selectWave=SoftTone(true);moveSound=new SoundPlayer(moveWave);selectSound=new SoundPlayer(selectWave);moveSound.Load();selectSound.Load();}(select?selectSound:moveSound).Play();}catch{ /* Audio is optional; keep navigation responsive. */ }
 }
 void DisposePolish(){DisposeArrival();if(openingBackground!=null)openingBackground.Dispose();if(openingTitle!=null)openingTitle.Dispose();if(moveSound!=null)moveSound.Dispose();if(selectSound!=null)selectSound.Dispose();if(moveWave!=null)moveWave.Dispose();if(selectWave!=null)selectWave.Dispose();}
 void PaintReveal(Graphics g){}
 void PaintOpeningGrid(Graphics g,float curvature,float alpha){
  int count=lib.Stereo?2:1;float width=ClientSize.Width/(float)count;
  if(gpu!=null){
   if(openingBackground==null){openingBackground=new Bitmap(1440,940,PixelFormat.Format32bppPArgb);using(var bg=Graphics.FromImage(openingBackground))DrawBackdropBase(bg);}
   gpu.Begin(ClientSize.Width,ClientSize.Height);
   for(int eye=0;eye<count;eye++){var pane=new RectangleF(eye*width,0,width,ClientSize.Height);gpu.Clip((int)pane.X,0,(int)width,ClientSize.Height);gpu.Image(openingBackground,pane,alpha);gpu.OpeningGrid(Eye(eye,count),curvature,pane,alpha,headY);}gpu.EndClip();
  }else{
   using(var layer=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb)){
    using(var bg=Graphics.FromImage(layer)){bg.SmoothingMode=SmoothingMode.AntiAlias;for(int eye=0;eye<count;eye++){var state=bg.Save();bg.SetClip(new RectangleF(eye*width,0,width,ClientSize.Height));bg.TranslateTransform(eye*width,0);bg.ScaleTransform(width/1440,ClientSize.Height/940f);DrawBackdrop(bg,Eye(eye,count),curvature);bg.Restore(state);}}
    using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attributes.SetColorMatrix(matrix);g.DrawImage(layer,ClientRectangle,0,0,layer.Width,layer.Height,GraphicsUnit.Pixel,attributes);}
   }
  }
 }
 void RenderOpening(Graphics g){
  if(gpu!=null){gpu.Begin(ClientSize.Width,ClientSize.Height);gpu.Black();}else g.Clear(Color.Black);
  if(startupOpening&&gpu!=null)gpu.PrepareTitle();
  // Never expose even a flat SBS pattern before Hub confirms conversion.
  if(startupOpening&&!libraryMasked&&introSince>=0){float fade=Smooth((frameClock.ElapsedMilliseconds-(introSince-IntroSettle))/(float)IntroSettle);PaintOpeningGrid(g,0,fade);}
  if(startupOpening&&!libraryMasked&&introSince>=0&&frameClock.ElapsedMilliseconds>=introSince){
   if(openingTitle==null){openingTitle=new GraphicsPath();using(var font=new FontFamily("Segoe UI"))openingTitle.AddString("dEPTH",font,(int)FontStyle.Bold,1,PointF.Empty,StringFormat.GenericTypographic);}
   float t=Math.Max(0,Math.Min(1,(frameClock.ElapsedMilliseconds-introSince-IntroHold)/(float)IntroFlight));
   float curvature=Smooth(t/.82f);PaintOpeningGrid(g,curvature,1);
   float z=IntroDepth(t),scale=900/(900-z);int count=lib.Stereo?2:1;float width=ClientSize.Width/(float)count;
   for(int eye=0;eye<count;eye++){
    float disparity=StereoMath.Parallax(Eye(eye,count),z);
    float em=ClientSize.Height*.11f*scale,cx=eye*width+width/2+disparity*width/1440,cy=ClientSize.Height/2f;
    if(gpu!=null){gpu.Clip((int)(eye*width),0,(int)width,ClientSize.Height);gpu.SpatialTitle(Eye(eye,count),ClientSize.Width*940f/(ClientSize.Height*1440f),z,(int)(eye*width),(int)width,ClientSize.Height);}
    else{var state=g.Save();g.SetClip(new RectangleF(eye*width,0,width,ClientSize.Height));g.SmoothingMode=SmoothingMode.AntiAlias;var bounds=openingTitle.GetBounds();em=Math.Min(em,width*.72f*count/Math.Max(.1f,bounds.Width));g.TranslateTransform(cx,cy);g.ScaleTransform(em/count,em);g.TranslateTransform(-bounds.X-bounds.Width/2,-bounds.Y-bounds.Height/2);g.FillPath(Brushes.White,openingTitle);g.Restore(state);}
   }
   if(gpu!=null){gpu.EndClip();gpu.Begin(ClientSize.Width,ClientSize.Height);}
   float fade=Smooth((t-.82f)/.18f);if(fade>0)PaintOpeningGrid(g,curvature,fade);
  }
  if(startupOpening&&!libraryMasked&&introSince>=0)PaintOpeningBars(g,Smooth((frameClock.ElapsedMilliseconds-(introSince-IntroSettle))/(float)IntroSettle));
  if(gpu!=null)gpu.Finish(false);
  if(startupOpening&&libraryMasked)PrepareOpeningAssets();
 }
 public static float Smooth(float progress){float t=Math.Max(0,Math.Min(1,progress));return t*t*t*(t*(t*6-15)+10);}
 public static float IntroDepth(float progress){return 600*Smooth(progress/.82f);}
}
