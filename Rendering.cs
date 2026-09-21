using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class StereoMath {
 public static PointF Pointer(Point p,Size size){return new PointF(p.X*1440f/Math.Max(1,size.Width),p.Y*940f/Math.Max(1,size.Height));}
 public static float Parallax(float eye,float z){return eye*(1-900/(900-z));}
}

public partial class DepthWindow {
 GpuRenderer gpu;bool gpuAttempted=false;Bitmap pointerSprite;
 bool measureGpu=Environment.GetCommandLineArgs().Contains("--gpu-benchmark");List<double> gpuTimes=new List<double>();
 Bitmap chrome;string chromeKey="";float spriteAspect;
 Dictionary<Game,Bitmap> sprites=new Dictionary<Game,Bitmap>();
 PointF pointer=new PointF(720,470);bool pointerInside=false;
 Stopwatch frameClock=Stopwatch.StartNew();double lastFrame;
 Cursor transparentCursor;IntPtr cursorHandle;
 [DllImport("user32.dll")] static extern IntPtr CreateCursor(IntPtr instance,int x,int y,int width,int height,byte[] andMask,byte[] xorMask);
 [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
 [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
 void InitializeRendering(){
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Opaque,true);
  byte[] mask=Enumerable.Repeat((byte)255,128).ToArray();cursorHandle=CreateCursor(IntPtr.Zero,0,0,32,32,mask,new byte[128]);
  if(cursorHandle!=IntPtr.Zero){transparentCursor=new Cursor(cursorHandle);Cursor=transparentCursor;}
  timeBeginPeriod(1);pointerSprite=new Bitmap(64,64,PixelFormat.Format32bppPArgb);using(var p=Graphics.FromImage(pointerSprite)){var old=pointer;pointer=new PointF(5,5);PaintPointer(p,0);pointer=old;}
 }
 protected override void Dispose(bool disposing){
  if(disposing){headTracker.Dispose();DisposeCompanion();DisposePolish();DisposeTransitions();animation.Dispose();if(gpu!=null){gpu.Dispose();gpu=null;}if(pointerSprite!=null){pointerSprite.Dispose();pointerSprite=null;}if(chrome!=null){chrome.Dispose();chrome=null;}foreach(Bitmap b in sprites.Values)b.Dispose();sprites.Clear();foreach(Image i in images.Values)if(i!=null)i.Dispose();images.Clear();if(transparentCursor!=null){transparentCursor.Dispose();transparentCursor=null;}if(cursorHandle!=IntPtr.Zero){DestroyCursor(cursorHandle);cursorHandle=IntPtr.Zero;}timeEndPeriod(1);}
  base.Dispose(disposing);
 }
 Game FocusedCover {get{return over??selected;}}
 SizeF CoverSize(Game game){
  Image img=game==null?null:GetCover(game);
  float ratio=img==null?1:img.Width/(float)img.Height;
  float aspect=ClientSize.Width*940f/(Math.Max(1,ClientSize.Height)*1440f);
  float width=Math.Min(220,204*ratio/aspect);
  return new SizeF(width,width*aspect/ratio);
 }
 float Hover(Game g){float h;return hover.TryGetValue(g,out h)?h:0;}
 RectangleF CardBounds(int slot,float amount,float eye){
  // Scale around the cover's center, keeping hover stable under the pointer.
  float trackingGain=.55f-.30f*Math.Max(0,Math.Min(1,amount));
  eye-=headX*(1-trackingGain);
  float z=12+amount*180,k=900/(900-z),cx=186+(slot%5)*260,cy=423+(slot/5)*255+StereoMath.Parallax(headY*trackingGain,z);
  int index=page*10+slot;SizeF size=CoverSize(index>=0&&index<shown.Count?shown[index]:null);
  return new RectangleF(cx-size.Width*k/2+StereoMath.Parallax(eye,z),cy-size.Height*k/2,size.Width*k,size.Height*k);
 }
 void RebuildHits(){hits.Clear();for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++)hits[shown[i]]=CardBounds(i-page*10,Hover(shown[i]),headX);}
 Game GameAt(PointF p){if(FocusedCover!=null&&hits.ContainsKey(FocusedCover)&&hits[FocusedCover].Contains(p))return FocusedCover;foreach(var pair in hits)if(pair.Value.Contains(p))return pair.Key;return null;}
 void MovePointer(Point p){if(libraryMasked||introSince>=0||revealSince>=0)return;InvalidatePointer();pointer=Logical(p);pointerInside=true;RebuildHits();Game next=GameAt(pointer);if(next!=over){InvalidateCard(over);over=next;InvalidateLogical(new RectangleF(40,140,1360,90));if(next!=null)UiSound(false);InvalidateCard(over);}InvalidatePointer();}
 void InvalidateLogical(RectangleF r){
  int count=lib.Stereo?2:1;float w=ClientSize.Width/(float)count;
  for(int i=0;i<count;i++){Rectangle pixels=Rectangle.Ceiling(new RectangleF(i*w+r.X*w/1440,r.Y*ClientSize.Height/940,r.Width*w/1440,r.Height*ClientSize.Height/940));pixels.Inflate(3,3);Invalidate(pixels);}
 }
 void InvalidatePointer(){InvalidateLogical(new RectangleF(pointer.X-16,pointer.Y-16,75,80));}
 void InvalidateCard(Game game){if(game==null)return;int slot=shown.IndexOf(game)-page*10;if(slot<0||slot>=10)return;RectangleF r=CardBounds(slot,1,headX);r.Inflate(16,20);InvalidateLogical(r);}
 void AnimateFrame(){
  double now=frameClock.Elapsed.TotalSeconds,dt=Math.Min(.05,now-lastFrame);lastFrame=now;UpdateHeadTracking(dt);
  if(WindowState==FormWindowState.Minimized||!Visible)return;
  // Hub needs an actively presenting fullscreen surface to start conversion.
  // Returning with settled cover animations otherwise presents black only once,
  // then waits for Hub while Hub waits for frames. Keep black frames flowing.
  if(libraryMasked){Invalidate();return;}
  if(introSince>=0){if(frameClock.ElapsedMilliseconds-introSince>=IntroDuration){introSince=-1;startupOpening=false;revealSince=frameClock.ElapsedMilliseconds;FocusGame();}Invalidate();return;}
  if(revealSince>=0){if(frameClock.ElapsedMilliseconds-revealSince>=ArrivalDuration){revealSince=-1;}Invalidate();}
  AnimateConsole(now);
  if(measureGpu){if(shown.Count>0){over=shown[0];hover[over]=(float)(.5+.5*Math.Sin(now*8));}Invalidate();return;}
  float blend=(float)(1-Math.Exp(-dt*32));
  for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++){Game game=shown[i];float value=Hover(game),target=game==FocusedCover?1:0;if(Math.Abs(value-target)<.002f){if(value!=target){hover[game]=target;InvalidateCard(game);}continue;}hover[game]=value+(target-value)*blend;InvalidateCard(game);InvalidatePointer();}
 }
 string SceneKey(){return ClientSize+"|"+lib.Stereo+"|"+lib.Depth+"|"+lib.SwapEyes+"|"+lib.HeadTracking+"|"+page+"|"+filter+"|"+query+"|"+shown.Count+"|"+(selected==null?"":selected.Title+selected.Note)+"|"+(FocusedCover==null?"":FocusedCover.Title)+"|"+status+"|"+panel+"|"+menuIndex+"|"+keyIndex+"|"+editText+"|"+upper+"|"+panelMessage;}
 void ClearRenderCache(){chromeKey="";coverPaths.Clear();DisposeArrival();foreach(Bitmap b in sprites.Values){if(gpu!=null)gpu.Forget(b);b.Dispose();}sprites.Clear();foreach(Image i in images.Values)if(i!=null)i.Dispose();images.Clear();Invalidate();}
 Bitmap Sprite(Game game){Bitmap b;if(sprites.TryGetValue(game,out b))return b;SizeF size=CoverSize(game);b=new Bitmap(Math.Max(1,(int)Math.Ceiling(size.Width*2)),Math.Max(1,(int)Math.Ceiling(size.Height*2)),PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(b)){g.ScaleTransform(b.Width/size.Width,b.Height/size.Height);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;using(var clip=Rounded(new RectangleF(PointF.Empty,size),8)){g.SetClip(clip);DrawCardArtwork(g,game);}}sprites.Add(game,b);return b;}
 void RenderFast(Graphics g){
  if(ClientSize.Width<1||ClientSize.Height<1)return;float aspect=ClientSize.Width/(float)ClientSize.Height;if(Math.Abs(aspect-spriteAspect)>.0001f){foreach(Bitmap b in sprites.Values){if(gpu!=null)gpu.Forget(b);b.Dispose();}sprites.Clear();spriteAspect=aspect;}
  if(Visible&&!gpuAttempted){gpuAttempted=true;try{gpu=new GpuRenderer(Handle);DoubleBuffered=false;System.IO.File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"renderer.txt"),"GPU: "+gpu.Device+"; MSAA: "+gpu.AntialiasSamples+"x");}catch(Exception ex){if(gpu!=null){gpu.Dispose();gpu=null;}System.IO.File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"renderer.txt"),"Software fallback: "+ex.Message);}}
  if(libraryMasked||introSince>=0){RenderOpening(g);return;}
  if(revealSince>=0){RenderArrival(g);return;}
  int count=lib.Stereo?2:1;float w=ClientSize.Width/(float)count;
  PrepareChrome(count,w);
  if(gpu!=null){if(panel=="")PrepareArrival();RenderGpu(count,w);return;}
  g.DrawImageUnscaled(chrome,0,0);if(panel!="")return;RebuildHits();
  for(int eye=0;eye<count;eye++){
   var state=g.Save();g.SetClip(new RectangleF(eye*w,0,w,ClientSize.Height),CombineMode.Intersect);g.TranslateTransform(eye*w,0);g.ScaleTransform(w/1440f,ClientSize.Height/940f);g.InterpolationMode=InterpolationMode.Bilinear;g.PixelOffsetMode=PixelOffsetMode.Half;
   DrawConsole(g,eye,count,w);
   for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++)if(shown[i]!=FocusedCover)PaintCover(g,shown[i],i-page*10,Eye(eye,count));
   if(FocusedCover!=null){int slot=shown.IndexOf(FocusedCover)-page*10;if(slot>=0&&slot<10)PaintCover(g,FocusedCover,slot,Eye(eye,count));}
   if(pointerInside)PaintPointer(g,Eye(eye,count));g.Restore(state);
  }

 }
 void PrepareChrome(int count,float w){
  string key=gpu!=null&&panel==""?ClientSize+"|"+lib.Stereo+"|"+lib.Depth+"|"+lib.SwapEyes:SceneKey();if(chrome==null||chromeKey!=key){
   if(chrome!=null){if(gpu!=null)gpu.Forget(chrome);chrome.Dispose();}chrome=new Bitmap(ClientSize.Width,ClientSize.Height,PixelFormat.Format32bppPArgb);if(gpu==null||panel!="")buttons.Clear();if(panel!="")DisposeArrival();
   using(var bg=Graphics.FromImage(chrome)){
    bg.SmoothingMode=SmoothingMode.AntiAlias;bg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    for(int eye=0;eye<count;eye++){var state=bg.Save();bg.SetClip(new RectangleF(eye*w,0,w,ClientSize.Height));bg.TranslateTransform(eye*w,0);bg.ScaleTransform(w/1440f,ClientSize.Height/940f);if(gpu!=null&&panel=="")DrawBackdropBase(bg);else DrawScene(bg,Eye(eye,count));bg.Restore(state);}
   }
   chromeKey=key;
  }
 }
 protected override void OnHandleDestroyed(EventArgs e){if(gpu!=null){gpu.Dispose();gpu=null;}gpuAttempted=false;openingAssetsPrepared=false;base.OnHandleDestroyed(e);}
 RectangleF Pixels(RectangleF r,int eye,float w){return new RectangleF(eye*w+r.X*w/1440,r.Y*ClientSize.Height/940,r.Width*w/1440,r.Height*ClientSize.Height/940);}
 void RenderGpu(int count,float w){
  var sw=Stopwatch.StartNew();gpu.Begin(ClientSize.Width,ClientSize.Height);gpu.Image(chrome,new RectangleF(0,0,ClientSize.Width,ClientSize.Height));if(panel!=""){gpu.Finish(false);return;}RebuildHits();
  for(int eye=0;eye<count;eye++){
   gpu.Clip((int)(eye*w),0,(int)w,ClientSize.Height);gpu.OpeningGrid(Eye(eye,count),1,new RectangleF(eye*w,0,w,ClientSize.Height),1,headY);DrawConsole(null,eye,count,w);
   for(int layer=0;layer<arrivalLayers.Length;layer++)gpu.Image(arrivalLayers[layer],Pixels(arrivalRegions[layer],eye,w));
   for(int i=page*10;i<Math.Min(shown.Count,page*10+10);i++)if(shown[i]!=FocusedCover)GpuCover(shown[i],i-page*10,eye,count,w);
   if(FocusedCover!=null){int slot=shown.IndexOf(FocusedCover)-page*10;if(slot>=0&&slot<10)GpuCover(FocusedCover,slot,eye,count,w);}
   if(pointerInside){float z=over==null?6:12+Hover(over)*180;gpu.Image(pointerSprite,Pixels(new RectangleF(pointer.X+StereoMath.Parallax(Eye(eye,count)-headX,z)-5,pointer.Y-5,64,64),eye,w));}
   gpu.EndClip();
  }
  gpu.Finish(measureGpu);sw.Stop();
  if(measureGpu){gpuTimes.Add(sw.Elapsed.TotalMilliseconds);if(gpuTimes.Count==121){var times=gpuTimes.Skip(1).OrderBy(x=>x).ToArray();System.IO.File.WriteAllText(System.IO.Path.Combine(Storage.Folder,"gpu-benchmark.txt"),string.Format("{0} x {1} stereo, {2}; 120 animated full frames: median {3:F2} ms, p95 {4:F2} ms (GPU synchronized).",ClientSize.Width,ClientSize.Height,gpu.Device,times[60],times[114]));BeginInvoke((Action)(()=>Close()));}}
 }
 void GpuCover(Game game,int slot,int eye,int count,float w){RectangleF r=Pixels(CardBounds(slot,Hover(game),Eye(eye,count)),eye,w);gpu.Image(Sprite(game),r);gpu.Outline(r,game==over||game==selected?Color.FromArgb(160,255,232):Color.FromArgb(125,207,234),game==over?3:1,r.Width/CoverSize(game).Width*8,r.Height/CoverSize(game).Height*8);}
 float Eye(int i,int count){return headX+(count==1?0:(i==0?-26:26)*lib.Depth*(lib.SwapEyes?-1:1));}
 void PaintCover(Graphics g,Game game,int slot,float eye){
  float h=Hover(game);RectangleF r=CardBounds(slot,h,eye);
  RectangleF visible=r;visible.Inflate(12,18);if(!g.IsVisible(visible))return;
  using(var shadow=Rounded(new RectangleF(r.X+4,r.Y+7+h*6,r.Width,r.Height),r.Height*8/CoverSize(game).Height))using(var brush=new SolidBrush(Color.FromArgb(55,0,15,26)))g.FillPath(brush,shadow);g.DrawImage(Sprite(game),r);
  using(var pen=new Pen(game==over||game==selected?Color.FromArgb(160,255,232):Color.FromArgb(125,207,234,237),game==over?2.5f:1))using(var border=Rounded(r,r.Height*8/CoverSize(game).Height))g.DrawPath(pen,border);
 }
 void PaintPointer(Graphics g,float eye){
  float z=over==null?6:12+Hover(over)*180;float x=pointer.X+StereoMath.Parallax(eye,z),y=pointer.Y;
  var s=g.Save();g.TranslateTransform(x,y);g.SmoothingMode=SmoothingMode.AntiAlias;
  PointF[] arrow={new PointF(0,0),new PointF(0,29),new PointF(8,22),new PointF(14,36),new PointF(20,33),new PointF(14,20),new PointF(25,20)};
  using(var shadow=new SolidBrush(Color.FromArgb(140,8,40,62))){g.TranslateTransform(3,4);g.FillPolygon(shadow,arrow);g.TranslateTransform(-3,-4);}
  using(var face=new LinearGradientBrush(new Rectangle(0,0,25,36),Color.White,Color.FromArgb(58,218,198),45f))g.FillPolygon(face,arrow);
  using(var edge=new Pen(Color.FromArgb(9,61,77),1.6f))g.DrawPolygon(edge,arrow);
  using(var glint=new Pen(Color.FromArgb(220,255,255,255),1.2f))g.DrawLine(glint,2,5,2,23);
  g.Restore(s);
 }
}
