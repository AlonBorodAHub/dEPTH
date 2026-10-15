using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Runtime.InteropServices;

sealed class CompanionVideoView:IDisposable {
 readonly object gate=new object();ManualResetEvent cancel;Thread decoder;MenuAudioTrack audio;
 byte[] pixels;int width,height;long serial,copied,presented;Bitmap frame;volatile bool started;float volume;
 public string PathName="";public volatile string Error="";public volatile bool Ready;public int Loops;
 public Action<Bitmap> ForgetImage;public Game Game;public CompanionMedia Media;
 public bool Playing {get{return Ready&&started&&Error==""&&PathName!="";}}
 public double PositionSeconds {get{return audio==null?0:(audio.PositionMilliseconds%Math.Max(1,audio.Duration))/1000.0;}}
 public double VolumeLevel {get{return Playing?volume:0;}}
 public bool HasAudio {get{return audio!=null;}}
 public long FrameSerial {get{lock(gate){return serial;}}}
 public void Open(string path,Game game,CompanionMedia media){
  Stop();PathName=path;Game=game;Media=media;Error="";Loops=0;cancel=new ManualResetEvent(false);
  try{audio=new MenuAudioTrack(System.IO.Path.ChangeExtension(path,".wav"),true);audio.SetVolume(.12f);decoder=new Thread(Decode){IsBackground=true,Name="dEPTH preview video"};decoder.Start();}catch{Stop();throw;}
 }
 void Decode(){
  try{using(var reader=new PreviewFrameReader(PathName)){
   byte[] next=new byte[reader.Width*reader.Height*4];long stamp,cycle=0;int duration=audio.Duration;
   lock(gate){width=reader.Width;height=reader.Height;pixels=new byte[next.Length];}
   while(!cancel.WaitOne(0)){
    if(!reader.Read(next,out stamp)){reader.Rewind();cycle++;continue;}
    long due=cycle*duration+stamp;
    if(serial>0){while(!cancel.WaitOne(5)){if(started&&audio.PositionMilliseconds>=due)break;}if(cancel.WaitOne(0))return;}
    lock(gate){Buffer.BlockCopy(next,0,pixels,0,next.Length);serial++;Loops=(int)cycle;Ready=true;}
   }
  }}catch(Exception ex){if(!cancel.WaitOne(0))Error=ex.Message;}
 }
 public bool Present(double opacity){if(!Ready||Error!="")return false;volume=(float)(.12*Math.Max(0,Math.Min(1,opacity)));audio.SetVolume(volume);if(!started){audio.Play();started=true;}lock(gate){if(presented==serial)return false;presented=serial;return true;}}
 public Bitmap Frame(out bool changed){lock(gate){changed=false;if(pixels==null||!Ready)return null;if(frame==null)frame=new Bitmap(width,height,PixelFormat.Format32bppPArgb);if(copied!=serial){var data=frame.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);try{Marshal.Copy(pixels,0,data.Scan0,pixels.Length);}finally{frame.UnlockBits(data);}copied=serial;changed=true;}return frame;}}
 public void Stop(){
  if(cancel!=null)cancel.Set();if(decoder!=null){decoder.Join();decoder=null;}if(audio!=null){audio.Dispose();audio=null;}if(cancel!=null){cancel.Dispose();cancel=null;}
  lock(gate){if(frame!=null){if(ForgetImage!=null)ForgetImage(frame);frame.Dispose();frame=null;}pixels=null;serial=copied=presented=0;Ready=started=false;PathName="";}
 }
 public void Dispose(){Stop();}
}

public partial class DepthWindow {
 CompanionVideoView companionVideo;Game previewCandidate;long previewCandidateSince=-1;string failedPreview="";
 float previewFadePhase,previewOpacity;long previewFadeStamp=-1;
 void StopCompanionVideo(){previewFadePhase=previewOpacity=0;previewFadeStamp=-1;if(companionVideo!=null)companionVideo.Stop();previewCandidate=null;previewCandidateSince=-1;}
 void DisposeCompanionVideo(){if(companionVideo!=null){companionVideo.Dispose();companionVideo=null;}}
 void UpdateCompanionVideo(Game game,bool allowed,float alpha){
  if(!allowed){StopCompanionVideo();return;}
  if(companionVideo!=null&&companionVideo.Error!=""){FailCompanionPreview(companionVideo.PathName,companionVideo.Game,companionVideo.Error);}
  // Keep decoding during the fade so a quick return resumes without a restart.
  long now=frameClock.ElapsedMilliseconds;float dt=previewFadeStamp<0?0:Math.Max(0,(now-previewFadeStamp)/1000f);previewFadeStamp=now;
  previewFadePhase=Math.Max(0,Math.Min(1,previewFadePhase+(game==null?-dt/.65f:dt/.3f)));
  float opacity=Smooth(previewFadePhase);if(opacity!=previewOpacity){previewOpacity=opacity;Invalidate();}
  if(game==null&&previewFadePhase<=0){if(companionVideo!=null)companionVideo.Stop();}
  else if(companionVideo!=null&&companionVideo.Present(alpha*previewOpacity))Invalidate();
  if(game==null){previewCandidate=null;previewCandidateSince=-1;return;}
  if(previewCandidate!=game){previewCandidate=game;previewCandidateSince=frameClock.ElapsedMilliseconds;failedPreview="";}
  if(frameClock.ElapsedMilliseconds-previewCandidateSince<260)return;
  var media=MediaFor(game);
  if(string.IsNullOrEmpty(media.Video)){if(companionVideo!=null)companionVideo.Stop();return;}
  string path=Path.GetFullPath(Path.Combine(Storage.Folder,media.Video));
  if(!File.Exists(path)||path==failedPreview){if(companionVideo!=null)companionVideo.Stop();return;}
  try{
   if(companionVideo==null){companionVideo=new CompanionVideoView();companionVideo.ForgetImage=image=>{if(gpu!=null)gpu.Forget(image);};}
   if(companionVideo.PathName!=path)companionVideo.Open(path,game,media);
  }catch(Exception ex){FailCompanionPreview(path,game,ex.Message);return;}
  if(companionVideo.Error!=""){FailCompanionPreview(path,game,companionVideo.Error);return;}
  if(companionVideo.Present(alpha*previewOpacity))Invalidate();
 }
 void FailCompanionPreview(string path,Game game,string error){
  failedPreview=path;if(companionVideo!=null)companionVideo.Stop();
  try{File.WriteAllText(Path.Combine(Storage.Folder,"preview-error.txt"),game.Title+": "+error);}catch{}
 }
}




