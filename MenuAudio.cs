using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// Small PCM buffers give each track its own gain, without changing the Windows
// audio-session volume shared with the video player and interface sounds.
public sealed class MenuAudioTrack : IDisposable {
 [StructLayout(LayoutKind.Sequential,Pack=2)] struct Format {public ushort Tag,Channels;public uint Rate,BytesPerSecond;public ushort Align,Bits,Extra;}
 [StructLayout(LayoutKind.Sequential)] struct Header {public IntPtr Data;public uint Length,Recorded;public IntPtr User;public uint Flags,Loops;public IntPtr Next,Reserved;}
 [StructLayout(LayoutKind.Sequential)] struct Position {public uint Type,Value,Unused;}
 [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device,uint id,ref Format format,IntPtr callback,IntPtr instance,uint flags);
 [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device,IntPtr header,uint size);
 [DllImport("winmm.dll")] static extern uint waveOutPause(IntPtr device);
 [DllImport("winmm.dll")] static extern uint waveOutRestart(IntPtr device);
 [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
 [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
 [DllImport("winmm.dll")] static extern uint waveOutGetPosition(IntPtr device,ref Position position,uint size);
 sealed class Chunk {public byte[] Samples;public GCHandle Pin;public IntPtr Header;public bool Prepared,Busy;}
 readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);
 readonly Chunk[] chunks=new Chunk[3];readonly uint headerSize=(uint)Marshal.SizeOf(typeof(Header));
 static readonly int flagsOffset=Marshal.OffsetOf(typeof(Header),"Flags").ToInt32();
 IntPtr device;Thread pump;byte[] pcm;int offset;bool loop,queued,paused,finished,disposed;
 float targetGain=1,mixGain=1;Exception pumpError;uint bytesPerSecond,sampleRate;
 public readonly int Duration;
 public MenuAudioTrack(string path,bool loop){
  this.loop=loop;
  try{
   Format format=new Format();
   using(var reader=new BinaryReader(File.OpenRead(path))){
    if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected RIFF audio");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected WAVE audio");
    while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=reader.BaseStream.Position+size;if(end>reader.BaseStream.Length)throw new InvalidDataException("Truncated audio");
     if(chunk=="fmt "){format.Tag=reader.ReadUInt16();format.Channels=reader.ReadUInt16();format.Rate=reader.ReadUInt32();format.BytesPerSecond=reader.ReadUInt32();format.Align=reader.ReadUInt16();format.Bits=reader.ReadUInt16();}
     else if(chunk=="data")pcm=reader.ReadBytes(checked((int)size));reader.BaseStream.Position=end+(size%2);
    }
   }
   if(format.Tag!=1||format.Bits!=16||format.BytesPerSecond==0||format.Align==0||pcm==null||pcm.Length==0||pcm.Length%format.Align!=0)throw new InvalidDataException("Expected aligned 16-bit PCM audio");
   Duration=(int)(pcm.Length*1000L/format.BytesPerSecond);bytesPerSecond=format.BytesPerSecond;sampleRate=format.Rate;
   Check(waveOutOpen(out device,uint.MaxValue,ref format,wake.SafeWaitHandle.DangerousGetHandle(),IntPtr.Zero,0x50000));
   int bytes=Math.Max(format.Align,(int)(format.BytesPerSecond/50/format.Align)*format.Align);
   for(int i=0;i<chunks.Length;i++){
    var chunk=new Chunk{Samples=new byte[bytes]};chunks[i]=chunk;chunk.Pin=GCHandle.Alloc(chunk.Samples,GCHandleType.Pinned);chunk.Header=Marshal.AllocHGlobal((int)headerSize);
    Marshal.StructureToPtr(new Header{Data=chunk.Pin.AddrOfPinnedObject(),Length=(uint)bytes},chunk.Header,false);Check(waveOutPrepareHeader(device,chunk.Header,headerSize));chunk.Prepared=true;
   }
   pump=new Thread(Pump){IsBackground=true,Name="dEPTH PCM audio"};pump.Start();
  }catch{Dispose();throw;}
 }
 static void Check(uint result){if(result!=0)throw new InvalidOperationException("Windows PCM audio error "+result);}
 void CheckPump(){if(pumpError!=null)throw new InvalidOperationException("PCM playback failed",pumpError);}
 void Pump(){
  while(true){wake.WaitOne(100);lock(gate){if(disposed)return;if(!queued)continue;
   try{bool busy=false;foreach(var chunk in chunks){if(chunk.Busy&&(Marshal.ReadInt32(chunk.Header,flagsOffset)&1)!=0)chunk.Busy=false;if(!chunk.Busy)Queue(chunk);busy|=chunk.Busy;}if(!busy)finished=true;}
   catch(Exception ex){pumpError=ex;queued=false;waveOutReset(device);}
  }}
 }
 void Queue(Chunk chunk){
  if(!loop&&offset>=pcm.Length)return;
  float start=mixGain,end=targetGain;int samples=chunk.Samples.Length/2;
  for(int i=0;i<samples;i++){
   if(offset>=pcm.Length){if(loop)offset=0;else{Array.Clear(chunk.Samples,i*2,chunk.Samples.Length-i*2);break;}}
   short original=(short)(pcm[offset]|(pcm[offset+1]<<8));float gain=start+(end-start)*(i+1)/samples;short value=(short)(original*gain);
   chunk.Samples[i*2]=(byte)(value&255);chunk.Samples[i*2+1]=(byte)((value>>8)&255);offset+=2;
  }
  mixGain=end;Check(waveOutWrite(device,chunk.Header,headerSize));chunk.Busy=true;
 }
 public bool Playing {get{lock(gate){return !disposed&&queued&&!paused&&!finished&&pumpError==null;}}}
 public uint PositionMilliseconds {get{lock(gate){var p=new Position{Type=1};Check(waveOutGetPosition(device,ref p,(uint)Marshal.SizeOf(typeof(Position))));return p.Type==4?(uint)(p.Value*1000UL/bytesPerSecond):p.Type==2?(uint)(p.Value*1000UL/sampleRate):p.Value;}}}
 public void SetVolume(float gain){lock(gate){if(disposed)return;targetGain=Math.Max(0,Math.Min(1,gain));}}
 public void Play(){lock(gate){if(disposed)return;CheckPump();if(queued&&!finished){if(paused){Check(waveOutRestart(device));paused=false;}return;}offset=0;mixGain=targetGain;queued=true;paused=finished=false;foreach(var chunk in chunks)Queue(chunk);wake.Set();}}
 public void Pause(){lock(gate){if(disposed||!queued||paused)return;Check(waveOutPause(device));paused=true;}}
 public void Stop(){lock(gate){if(disposed||!queued)return;Check(waveOutReset(device));queued=paused=finished=false;offset=0;foreach(var chunk in chunks)chunk.Busy=false;}}
 public void Dispose(){
  lock(gate){if(disposed)return;disposed=true;if(device!=IntPtr.Zero)waveOutReset(device);wake.Set();}
  if(pump!=null&&pump!=Thread.CurrentThread)pump.Join();
  if(device!=IntPtr.Zero){foreach(var chunk in chunks)if(chunk!=null&&chunk.Prepared)waveOutUnprepareHeader(device,chunk.Header,headerSize);waveOutClose(device);device=IntPtr.Zero;}
  foreach(var chunk in chunks)if(chunk!=null){if(chunk.Header!=IntPtr.Zero)Marshal.FreeHGlobal(chunk.Header);if(chunk.Pin.IsAllocated)chunk.Pin.Free();}wake.Dispose();pcm=null;
 }
}

public sealed class MenuAudio : IDisposable {
 MenuAudioTrack intro,menu;
 float menuGain=1,previewMixStart=1,previewMixTime=2;bool previewMixMuted;
 public void UpdatePreviewMix(bool previewPlaying,float dt){
  if(previewPlaying!=previewMixMuted){previewMixMuted=previewPlaying;previewMixStart=menuGain;previewMixTime=0;}
  float duration=previewMixMuted?.35f:1.1f;previewMixTime+=Math.Max(0,dt);
  float t=Math.Min(1,previewMixTime/duration);t=t*t*(3-2*t);
  float gain=previewMixStart+((previewMixMuted?0:1)-previewMixStart)*t;
  if(gain==menuGain)return;menuGain=gain;
  try{if(menu!=null)menu.SetVolume(menuGain);}catch(Exception ex){Log(ex);if(menu!=null){menu.Dispose();menu=null;}}
 }
 public MenuAudio(string directory){intro=Load(Path.Combine(directory,"startup.wav"),false);menu=Load(Path.Combine(directory,"menu.wav"),true);}
 static void Log(Exception ex){try{File.AppendAllText(Path.Combine(Storage.Folder,"audio-errors.txt"),DateTime.Now+" "+ex.Message+Environment.NewLine);}catch{}}
 static MenuAudioTrack Load(string path,bool loop){if(!File.Exists(path))return null;try{return new MenuAudioTrack(path,loop);}catch(Exception ex){Log(ex);return null;}}
 public void StartIntro(){SetMenu(false);try{if(intro!=null){intro.Stop();intro.Play();}}catch(Exception ex){Log(ex);intro.Dispose();intro=null;}}
 public void StopIntro(){try{if(intro!=null)intro.Stop();}catch(Exception ex){Log(ex);intro.Dispose();intro=null;}}
 public void SetMenu(bool playing){try{if(menu==null)return;if(playing){StopIntro();if(!menu.Playing)menu.Play();}else menu.Pause();}catch(Exception ex){Log(ex);menu.Dispose();menu=null;}}
 public void Dispose(){if(intro!=null)intro.Dispose();if(menu!=null)menu.Dispose();intro=null;menu=null;}
}

public partial class DepthWindow {
 MenuAudio menuAudio;double lastAudioMixFrame=-1;
 void PrepareMenuAudio(){if(menuAudio==null)menuAudio=new MenuAudio(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","audio"));}
 void UpdateMenuAudio(){
  if(menuAudio==null)return;
  double now=frameClock.Elapsed.TotalSeconds;float dt=lastAudioMixFrame<0?0:(float)Math.Min(.05,Math.Max(0,now-lastAudioMixFrame));lastAudioMixFrame=now;
  menuAudio.UpdatePreviewMix(companionVideo!=null&&companionVideo.Playing,dt);
  bool menuVisible=Visible&&WindowState!=System.Windows.Forms.FormWindowState.Minimized&&activeGame==null&&returnSince<0&&!libraryMasked&&introSince<0;
  if(!Visible||WindowState==System.Windows.Forms.FormWindowState.Minimized||activeGame!=null)menuAudio.StopIntro();
  menuAudio.SetMenu(menuVisible&&(revealSince<0||ArrivalTime>=ArrivalFirstCoverDelay));
 }
}




