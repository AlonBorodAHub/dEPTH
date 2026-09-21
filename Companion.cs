using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;

public class CompanionMedia {public string Image="",Summary="",Details="",ImageSource="",InfoSource="",Credit="";}
sealed class CompanionWindow:Form {
 public Action<Graphics,Size> Draw;
 public Action<GpuRenderer,Size> DrawGpu;GpuRenderer renderer;bool rendererAttempted;
 public void ForgetImage(Bitmap image){if(renderer!=null)renderer.Forget(image);}
 protected override void OnHandleDestroyed(EventArgs e){if(renderer!=null){renderer.Dispose();renderer=null;}rendererAttempted=false;base.OnHandleDestroyed(e);}
 protected override void Dispose(bool disposing){if(disposing&&renderer!=null){renderer.Dispose();renderer=null;}base.Dispose(disposing);}
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
 public CompanionWindow(){Text="dEPTH Companion";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;DoubleBuffered=true;SetStyle(ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);BackColor=Color.Black;TopMost=true;}
 protected override void OnPaint(PaintEventArgs e){long stamp=FrameTiming.Start();try{
  if(!rendererAttempted&&DrawGpu!=null){rendererAttempted=true;try{renderer=new GpuRenderer(Handle,false);DoubleBuffered=false;}catch{if(renderer!=null)renderer.Dispose();renderer=null;}}
  if(renderer!=null&&DrawGpu!=null)DrawGpu(renderer,ClientSize);else if(Draw!=null)Draw(e.Graphics,ClientSize);
 }finally{FrameTiming.End("companion paint",stamp);}}
}
public partial class DepthWindow {
 readonly Dictionary<string,Bitmap> companionFrames=new Dictionary<string,Bitmap>();
 CompanionWindow companion;Timer companionTimer;Game companionGame;string companionCover="";bool companionDim;Bitmap companionBackground;bool companionBuildBusy;Game companionFrameGame;string companionFrameCover="";Size companionFrameSize;
 long companionRevealSince=-1;float lastCompanionAlpha=-1;
 bool CompanionOpeningBlocked {get{return startupOpening||libraryMasked||introSince>=0||revealSince>=0;}}
 float CompanionAlpha(){return CompanionOpeningBlocked||AzaharPlaying()||secondaryReturnCurtain!=null||companionRevealSince<0?0:Smooth((frameClock.ElapsedMilliseconds-companionRevealSince)/4000f);}
 void InitializeCompanion(){companionTimer=new Timer{Interval=33};companionTimer.Tick+=(s,e)=>{long stamp=FrameTiming.Start();try{UpdateCompanion();}finally{FrameTiming.End("companion update",stamp);}};companionTimer.Start();}
 void DisposeCompanion(){if(companionTimer!=null)companionTimer.Dispose();if(companion!=null)companion.Dispose();var frames=new HashSet<Bitmap>(companionFrames.Values);if(companionBackground!=null)frames.Add(companionBackground);foreach(var frame in frames)frame.Dispose();companionFrames.Clear();}
 bool AzaharPlaying(){return activeGame!=null&&string.Equals(System.IO.Path.GetFileNameWithoutExtension(activeGame.Executable),"azahar",StringComparison.OrdinalIgnoreCase);}
 void UpdateCompanion(){
  if(!Visible)return;
  if(CompanionOpeningBlocked||secondaryReturnCurtain!=null)companionRevealSince=-1;else if(companionRevealSince<0)companionRevealSince=frameClock.ElapsedMilliseconds;
  var screens=Screen.AllScreens;int target=LowerDisplay(Screen.FromControl(this).Bounds,screens.Select(s=>s.Bounds).ToArray());
  if(target<0){if(companion!=null)companion.Hide();return;}
  if(companion==null){companion=new CompanionWindow();companion.Draw=PaintCompanion;companion.DrawGpu=PaintCompanionGpu;}
  Rectangle bounds=screens[target].Bounds;bool changed=companion.Bounds!=bounds;
  if(changed)companion.Bounds=bounds;
  if(AzaharPlaying()&&tabletPhase>=3&&IsWindow(tabletWindow)&&secondaryReturnCurtain==null){
   // Keep a painted black surface immediately beneath the touch window,
   // including exits initiated inside Azahar rather than through dEPTH.
   if(!companion.Visible){companion.Show();companion.Refresh();}
   else if(lastCompanionAlpha!=0){companion.Refresh();}
   lastCompanionAlpha=0;SetWindowPos(companion.Handle,tabletWindow,0,0,0,0,0x0013);return;
  }
  Game game=activeGame??FocusedCover;bool dim=activeGame!=null;string cover=game==null?"":game.Cover;
  if(game!=companionGame||cover!=companionCover||dim!=companionDim){companionGame=game;companionCover=cover;companionDim=dim;changed=true;}
  float alpha=CompanionAlpha();if(alpha!=lastCompanionAlpha){changed=true;lastCompanionAlpha=alpha;}companionTimer.Interval=33;
  PrepareCompanionFrame(bounds.Size);if(!companion.Visible){companion.Show();changed=true;}if(changed)companion.Invalidate();
  if(secondaryReturnCurtain!=null&&!CompanionOpeningBlocked&&activeGame==null&&companionBackground!=null&&companionFrameGame==companionGame&&companionFrameCover==companionCover&&companionFrameSize==bounds.Size){
   // Paint the successor before removing the last cover, then begin its fade.
   SetWindowPos(companion.Handle,new IntPtr(-1),0,0,0,0,0x0013);companion.Refresh();RemoveSecondaryCurtain();companionRevealSince=frameClock.ElapsedMilliseconds;lastCompanionAlpha=-1;companion.Invalidate();
  }
 }
 Dictionary<string,CompanionMedia> gameMedia;
 CompanionMedia MediaFor(Game game){if(gameMedia==null){try{gameMedia=Storage.Json.Deserialize<Dictionary<string,CompanionMedia>>(File.ReadAllText(Path.Combine(Storage.Folder,"companion-media.json")));}catch{gameMedia=new Dictionary<string,CompanionMedia>();}}CompanionMedia media;if(game!=null){if(gameMedia.TryGetValue(SafeName(game),out media))return media;if(game.Platform=="GameCube"&&gameMedia.TryGetValue("Wii_"+SafeName(game).Substring(9),out media))return media;}return new CompanionMedia();}
 Image CompanionImage(CompanionMedia media){if(string.IsNullOrEmpty(media.Image))return null;string path=Path.Combine(Storage.Folder,media.Image);if(!images.ContainsKey(path)){try{using(var file=Image.FromFile(path))images[path]=new Bitmap(file);}catch{images[path]=null;}}return images[path];}
 void PaintCompanionGpu(GpuRenderer renderer,Size size){
  renderer.Begin(size.Width,size.Height);float alpha=CompanionAlpha();
  if(alpha<=0||companionBackground==null){renderer.Black();renderer.Finish(false);return;}
  renderer.Image(companionBackground,new RectangleF(0,0,size.Width,size.Height));
  float opacity=alpha*(companionDim?50/255f:1);if(opacity<1)renderer.BlackOverlay(size.Width,size.Height,1-opacity);
  renderer.Finish(false);
 }
 void PaintCompanion(Graphics g,Size size){
  if(size.Width<=0||size.Height<=0)return;
  float openingAlpha=CompanionAlpha();if(openingAlpha<=0){g.Clear(Color.Black);return;}
  if(companionBackground!=null)g.DrawImageUnscaled(companionBackground,0,0);else g.Clear(Color.Black);
  if(companionDim)using(var shade=new SolidBrush(Color.FromArgb(205,0,0,0)))g.FillRectangle(shade,0,0,size.Width,size.Height);
  if(openingAlpha<1)using(var shade=new SolidBrush(Color.FromArgb((int)(255*(1-openingAlpha)),0,0,0)))g.FillRectangle(shade,0,0,size.Width,size.Height);
 }
 void PrepareCompanionFrame(Size size){
  if(CompanionOpeningBlocked||companionBuildBusy||size.Width<1||size.Height<1)return;
  if(companionBackground!=null&&companionFrameGame==companionGame&&companionFrameCover==companionCover&&companionFrameSize==size)return;
  var game=companionGame;string cover=companionCover;var media=MediaFor(game);
  string cacheKey=(game==null?"":game.Key+"|"+game.Title)+"|"+cover+"|"+media.Image+"|"+size;Bitmap cached;
  if(companionFrames.TryGetValue(cacheKey,out cached)){companionBackground=cached;companionFrameGame=game;companionFrameCover=cover;companionFrameSize=size;companion.Invalidate();return;}
  companionBuildBusy=true;
  System.Threading.Tasks.Task.Factory.StartNew(()=>{
   Bitmap frame=null;
   try{frame=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(frame))DrawCompanionFrame(g,size,game,media);}
   catch{if(frame!=null)frame.Dispose();frame=null;}
   if(IsDisposed||Disposing){if(frame!=null)frame.Dispose();return;}
   try{BeginInvoke((Action)(()=>{companionBuildBusy=false;if(IsDisposed||game!=companionGame||cover!=companionCover||companion==null||companion.ClientSize!=size){if(frame!=null)frame.Dispose();return;}if(frame!=null){companionBackground=frame;companionFrames[cacheKey]=frame;
    if(companionFrames.Count>12){string victim=null;foreach(var pair in companionFrames)if(pair.Value!=frame){victim=pair.Key;break;}if(victim!=null){var old=companionFrames[victim];companion.ForgetImage(old);old.Dispose();companionFrames.Remove(victim);}}
    companionFrameGame=game;companionFrameCover=cover;companionFrameSize=size;companion.Invalidate();}}));}
   catch(InvalidOperationException){if(frame!=null)frame.Dispose();}
  });
 }
 void DrawCompanionFrame(Graphics g,Size size,Game game,CompanionMedia media){
  // Decode and resize screenshots away from the animation/input thread.
  Image art=null;try{if(!string.IsNullOrEmpty(media.Image))art=new Bitmap(Path.Combine(Storage.Folder,media.Image));}catch{}
  if(art!=null){using(art){g.InterpolationMode=InterpolationMode.HighQualityBicubic;float scale=Math.Max(size.Width/(float)art.Width,size.Height/(float)art.Height),w=art.Width*scale,h=art.Height*scale;g.DrawImage(art,new RectangleF((size.Width-w)/2,(size.Height-h)/2,w,h));}}
  else{var state=g.Save();g.ScaleTransform(size.Width/1440f,size.Height/940f);DrawBackdrop(g,0);g.Restore(state);}
  // White typography stays legible over bright gameplay without obscuring the scene.
  using(var shade=new LinearGradientBrush(new Rectangle(0,0,size.Width,size.Height),Color.FromArgb(10,0,0,0),Color.FromArgb(230,0,0,0),90f))g.FillRectangle(shade,0,0,size.Width,size.Height);
  float margin=size.Width*.055f,textWidth=size.Width*.86f,top=size.Height*.58f;
  g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
  using(var titleFont=new Font("Segoe UI",size.Height*.055f,FontStyle.Bold,GraphicsUnit.Pixel))
  using(var detailFont=new Font("Segoe UI",size.Height*.024f,FontStyle.Regular,GraphicsUnit.Pixel))
  using(var bodyFont=new Font("Segoe UI",size.Height*.026f,FontStyle.Regular,GraphicsUnit.Pixel))
  using(var format=new StringFormat{Trimming=StringTrimming.EllipsisWord}){
   g.DrawString(game==null?"dEPTH":game.Title,titleFont,Brushes.White,new RectangleF(margin,top,textWidth,size.Height*.14f),format);
   g.DrawString(string.IsNullOrEmpty(media.Details)?(game==null?"Library":game.Platform):media.Details,detailFont,Brushes.White,new RectangleF(margin,size.Height*.73f,textWidth,size.Height*.04f),format);
   g.DrawString(media.Summary,bodyFont,Brushes.White,new RectangleF(margin,size.Height*.79f,textWidth,size.Height*.14f),format);
  }
  using(var creditFont=new Font("Segoe UI",Math.Max(10,size.Height*.016f),GraphicsUnit.Pixel))
  using(var creditBrush=new SolidBrush(Color.FromArgb(185,255,255,255)))g.DrawString(media.Credit,creditFont,creditBrush,margin,size.Height*.955f);
 }
}
