using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Independent PCM devices mix with UI sounds without changing system volume.
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
 IntPtr device,header;GCHandle samples;bool prepared,queued,paused,disposed;
 readonly uint headerSize=(uint)Marshal.SizeOf(typeof(Header));
 public readonly int Duration;
 public MenuAudioTrack(string path,bool loop){
  try{
   Format format=new Format();byte[] pcm=null;
   using(var reader=new BinaryReader(File.OpenRead(path))){
    if(new string(reader.ReadChars(4))!="RIFF")throw new InvalidDataException("Expected RIFF audio");reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected WAVE audio");
    while(reader.BaseStream.Position+8<=reader.BaseStream.Length){string chunk=new string(reader.ReadChars(4));uint size=reader.ReadUInt32();long end=reader.BaseStream.Position+size;if(end>reader.BaseStream.Length)throw new InvalidDataException("Truncated audio");
     if(chunk=="fmt "){format.Tag=reader.ReadUInt16();format.Channels=reader.ReadUInt16();format.Rate=reader.ReadUInt32();format.BytesPerSecond=reader.ReadUInt32();format.Align=reader.ReadUInt16();format.Bits=reader.ReadUInt16();}
     else if(chunk=="data")pcm=reader.ReadBytes(checked((int)size));
     reader.BaseStream.Position=end+(size%2);
    }
   }
   if(format.Tag!=1||format.Bits!=16||format.BytesPerSecond==0||pcm==null||pcm.Length==0)throw new InvalidDataException("Expected 16-bit PCM audio");
   Duration=(int)(pcm.Length*1000L/format.BytesPerSecond);
   Check(waveOutOpen(out device,uint.MaxValue,ref format,IntPtr.Zero,IntPtr.Zero,0));
   samples=GCHandle.Alloc(pcm,GCHandleType.Pinned);header=Marshal.AllocHGlobal((int)headerSize);
   var value=new Header{Data=samples.AddrOfPinnedObject(),Length=(uint)pcm.Length,Flags=loop?12u:0u,Loops=loop?uint.MaxValue:0};
   Marshal.StructureToPtr(value,header,false);Check(waveOutPrepareHeader(device,header,headerSize));prepared=true;
  }catch{Dispose();throw;}
 }
 static void Check(uint result){if(result!=0)throw new InvalidOperationException("Windows PCM audio error "+result);}
 public bool Playing {get{return !disposed&&queued&&!paused&&(((Header)Marshal.PtrToStructure(header,typeof(Header))).Flags&1)==0;}}
 public uint PositionMilliseconds {get{var p=new Position{Type=1};Check(waveOutGetPosition(device,ref p,(uint)Marshal.SizeOf(typeof(Position))));return p.Value;}}
 public void Play(){if(disposed)return;if(queued){Check(waveOutRestart(device));paused=false;}else{Check(waveOutWrite(device,header,headerSize));queued=true;paused=false;}}
 public void Pause(){if(disposed||!queued||paused)return;Check(waveOutPause(device));paused=true;}
 public void Stop(){if(disposed||!queued)return;Check(waveOutReset(device));queued=false;paused=false;}
 public void Dispose(){if(disposed)return;if(device!=IntPtr.Zero){waveOutReset(device);if(prepared)waveOutUnprepareHeader(device,header,headerSize);waveOutClose(device);device=IntPtr.Zero;}if(header!=IntPtr.Zero){Marshal.FreeHGlobal(header);header=IntPtr.Zero;}if(samples.IsAllocated)samples.Free();disposed=true;}
}

public sealed class MenuAudio : IDisposable {
 MenuAudioTrack intro,menu;
 public MenuAudio(string directory){intro=Load(Path.Combine(directory,"startup.wav"),false);menu=Load(Path.Combine(directory,"menu.wav"),true);}
 static void Log(Exception ex){try{File.AppendAllText(Path.Combine(Storage.Folder,"audio-errors.txt"),DateTime.Now+" "+ex.Message+Environment.NewLine);}catch{}}
 static MenuAudioTrack Load(string path,bool loop){if(!File.Exists(path))return null;try{return new MenuAudioTrack(path,loop);}catch(Exception ex){Log(ex);return null;}}
 public void StartIntro(){SetMenu(false);try{if(intro!=null){intro.Stop();intro.Play();}}catch(Exception ex){Log(ex);intro.Dispose();intro=null;}}
 public void StopIntro(){try{if(intro!=null)intro.Stop();}catch(Exception ex){Log(ex);intro.Dispose();intro=null;}}
 public void SetMenu(bool playing){try{if(menu==null)return;if(playing){StopIntro();if(!menu.Playing)menu.Play();}else menu.Pause();}catch(Exception ex){Log(ex);menu.Dispose();menu=null;}}
 public void Dispose(){if(intro!=null)intro.Dispose();if(menu!=null)menu.Dispose();intro=null;menu=null;}
}

public partial class DepthWindow {
 MenuAudio menuAudio;
 void PrepareMenuAudio(){if(menuAudio==null)menuAudio=new MenuAudio(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","audio"));}
 void UpdateMenuAudio(){
  if(menuAudio==null)return;
  bool menuVisible=Visible&&WindowState!=System.Windows.Forms.FormWindowState.Minimized&&activeGame==null&&returnSince<0&&!libraryMasked&&introSince<0;
  if(!Visible||WindowState==System.Windows.Forms.FormWindowState.Minimized||activeGame!=null)menuAudio.StopIntro();
  menuAudio.SetMenu(menuVisible&&(revealSince<0||ArrivalTime>=ArrivalFirstCoverDelay));
 }
}
