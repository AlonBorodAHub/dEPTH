using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

static class DepthRecorder {
 const string RawRequestName=@"Local\dEPTH.RawSbsRecordingRequested",RawReadyName=@"Local\dEPTH.HubRawSbsReady";
 const int RecordingStartDelayMilliseconds=1000;
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
 [STAThread] public static int Main(string[] args){
  // gdigrab uses physical desktop pixels. Make WinForms report the same
  // coordinate space before reading Screen.PrimaryScreen.Bounds, otherwise
  // display scaling captures only the upper-left portion of the monitor.
  SetProcessDPIAware();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length>=3&&args[0]=="--capture-test"){Capture(args[1],Math.Max(1,int.Parse(args[2]))*1000,false);return 0;}
   string folder=AppDomain.CurrentDomain.BaseDirectory,depth=Path.Combine(folder,"Depth.exe");if(!File.Exists(depth))throw new FileNotFoundException("Depth.exe must be beside Depth Record.exe.",depth);
   if(DepthRunning(depth))throw new InvalidOperationException("Close the existing dEPTH session before starting a recording.");
   string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),stamp=DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
   string output=Unique(Path.Combine(desktop,"dEPTH Recording "+stamp+".mp4"));Capture(output,0,true);
   MessageBox.Show("Recording saved to your desktop:\n\n"+Path.GetFileName(output),"dEPTH Record",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;
  }catch(Exception ex){if(args.Length>=2&&args[0]=="--capture-test"){try{File.WriteAllText(args[1]+".error.txt",ex.ToString());}catch{}return 1;}MessageBox.Show(ex.Message,"dEPTH Record",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
 }
 static string Unique(string path){if(!File.Exists(path))return path;string folder=Path.GetDirectoryName(path),name=Path.GetFileNameWithoutExtension(path),ext=Path.GetExtension(path);for(int i=2;;i++){string candidate=Path.Combine(folder,name+" ("+i+")"+ext);if(!File.Exists(candidate))return candidate;}}
 static string Quote(string value){return "\""+value.Replace("\"","\\\"")+"\"";}
 static string FindFfmpeg(){
  string beside=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ffmpeg.exe"),jellyfin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Jellyfin\Server\ffmpeg.exe");
  if(File.Exists(beside))return beside;if(File.Exists(jellyfin))return jellyfin;
  string path=Environment.GetEnvironmentVariable("PATH")??"";foreach(string folder in path.Split(Path.PathSeparator)){try{string candidate=Path.Combine(folder.Trim(),"ffmpeg.exe");if(File.Exists(candidate))return candidate;}catch{}}
  throw new FileNotFoundException("FFmpeg was not found. Keep Jellyfin Server installed or place ffmpeg.exe beside Depth Record.exe.");
 }
 static bool ProbeNvenc(string ffmpeg,string log){
  string args="-hide_banner -loglevel error -f lavfi -i color=size=256x256:rate=1 -frames:v 1 -c:v h264_nvenc -f null NUL";
  using(var p=Start(ffmpeg,args,log,false)){if(!p.WaitForExit(10000)){p.Kill();return false;}return p.ExitCode==0;}
 }
 static Process Start(string executable,string arguments,string log,bool input){
  var info=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardInput=input};
  var process=new Process{StartInfo=info,EnableRaisingEvents=true};process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)try{lock(log)File.AppendAllText(log,e.Data+Environment.NewLine);}catch{}};
  if(!process.Start())throw new InvalidOperationException("Could not start "+Path.GetFileName(executable)+".");process.BeginErrorReadLine();return process;
 }
 static void Capture(string output,int testMilliseconds,bool launchDepth){
  string ffmpeg=FindFfmpeg(),temp=Path.Combine(Path.GetTempPath(),"depth-record-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  string video=Path.Combine(temp,"video.mkv"),audio=Path.Combine(temp,"audio.wav"),muxed=Path.Combine(temp,"recording.mp4"),log=Path.Combine(temp,"recording.log");
  LoopbackCapture loopback=null;Process recorder=null;EventWaitHandle rawRequest=null;bool success=false;
  try{
   if(launchDepth){rawRequest=new EventWaitHandle(false,EventResetMode.ManualReset,RawRequestName);rawRequest.Set();WaitForRawHub();}
   string probeLog=Path.Combine(temp,"encoder-probe.log");bool nvenc=ProbeNvenc(ffmpeg,probeLog);Rectangle screen=Screen.PrimaryScreen.Bounds;
   // GDI capture declared 60 fps but could deliver only about 20 unique 4K
   // frames per second. Desktop Duplication keeps the surface on the GPU and
   // NVENC's fastest low-latency preset sustains a paced 3840x2160/60 stream.
   string videoArgs=nvenc
    ?string.Format("-hide_banner -loglevel warning -y -f lavfi -i ddagrab=output_idx=0:draw_mouse=0:framerate=60:video_size={0}x{1}:dup_frames=true -an -c:v h264_nvenc -preset p1 -tune ull -rc constqp -qp 21 -zerolatency 1 -r 60 -fps_mode cfr -f matroska {2}",screen.Width,screen.Height,Quote(video))
    :string.Format("-hide_banner -loglevel warning -y -f gdigrab -framerate 60 -draw_mouse 0 -offset_x {0} -offset_y {1} -video_size {2}x{3} -i desktop -an -c:v libx264 -preset ultrafast -crf 20 -pix_fmt yuv420p -r 60 -fps_mode cfr -f matroska {4}",screen.X,screen.Y,screen.Width,screen.Height,Quote(video));
   string launchedDepth=null;
   if(launchDepth){launchedDepth=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Depth.exe");using(var started=Process.Start(new ProcessStartInfo(launchedDepth,"--raw-sbs-recording"){WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,UseShellExecute=false}))AllowSetForegroundWindow((uint)started.Id);Thread.Sleep(RecordingStartDelayMilliseconds);}
   loopback=new LoopbackCapture(audio);loopback.Start();recorder=Start(ffmpeg,videoArgs,log,true);
   Thread.Sleep(350);if(recorder.HasExited)throw new InvalidOperationException("Screen recording could not start.\n\n"+Tail(log));
   if(launchDepth)WaitForDepthSession(launchedDepth);
   else Thread.Sleep(testMilliseconds);
   StopRecorder(recorder);recorder=null;loopback.Stop();loopback.Dispose();loopback=null;
   if(!File.Exists(video)||new FileInfo(video).Length<1024)throw new InvalidOperationException("No screen frames were recorded.\n\n"+Tail(log)+"\nDiagnostic folder: "+temp);
   if(!File.Exists(audio)||new FileInfo(audio).Length<64)throw new InvalidOperationException("No system audio was recorded.\nDiagnostic folder: "+temp);
   string muxArgs="-hide_banner -loglevel warning -y -i "+Quote(video)+" -i "+Quote(audio)+" -map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -af aresample=async=1:first_pts=0 -shortest -movflags +faststart "+Quote(muxed);
   using(var mux=Start(ffmpeg,muxArgs,log,false)){if(!mux.WaitForExit(120000)){mux.Kill();throw new TimeoutException("Timed out while finishing the MP4 file.");}if(mux.ExitCode!=0||!File.Exists(muxed)||new FileInfo(muxed).Length<1024)throw new InvalidOperationException("The MP4 file could not be finalized.\n\n"+Tail(log)+"\nDiagnostic folder: "+temp);}
   string outputFolder=Path.GetDirectoryName(output);if(!Directory.Exists(outputFolder))Directory.CreateDirectory(outputFolder);if(File.Exists(output))File.Delete(output);File.Move(muxed,output);success=true;
  }finally{
   if(recorder!=null){try{StopRecorder(recorder);}catch{try{recorder.Kill();}catch{}}recorder.Dispose();}
   if(loopback!=null){try{loopback.Stop();}catch{}loopback.Dispose();}
   if(rawRequest!=null){try{rawRequest.Reset();}catch{}rawRequest.Dispose();}
   if(success)try{if(Directory.Exists(temp))Directory.Delete(temp,true);}catch{}
  }
 }
 static void WaitForRawHub(){
  string helper=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"dEPTH\HubSessionGuard.exe");if(!File.Exists(helper))return;
  var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<12000){try{using(var ready=EventWaitHandle.OpenExisting(RawReadyName)){if(ready.WaitOne(250))return;}}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(250);}}
  throw new TimeoutException("The dEPTH Hub helper did not switch to raw SBS recording mode.");
 }
 static string Tail(string path){try{string text=File.ReadAllText(path);return text.Length<=1800?text:text.Substring(text.Length-1800);}catch{return "See the recording log in the temporary folder.";}}
 static void StopRecorder(Process process){if(process.HasExited){if(process.ExitCode!=0)throw new InvalidOperationException("Screen recording stopped unexpectedly.");return;}process.StandardInput.WriteLine("q");process.StandardInput.Flush();if(!process.WaitForExit(15000)){process.Kill();process.WaitForExit();}if(process.ExitCode!=0)throw new InvalidOperationException("Screen recording did not finish cleanly.");process.Dispose();}
 static bool DepthRunning(string exact){foreach(var p in Process.GetProcessesByName("Depth"))using(p){try{if(!p.HasExited&&string.Equals(p.MainModule.FileName,exact,StringComparison.OrdinalIgnoreCase))return true;}catch{}}return false;}
 static void WaitForDepthSession(string exact){bool seen=false;long emptySince=-1;var clock=Stopwatch.StartNew();while(true){bool running=DepthRunning(exact);if(running){seen=true;emptySince=-1;}else if(seen){if(emptySince<0)emptySince=clock.ElapsedMilliseconds;if(clock.ElapsedMilliseconds-emptySince>=1500)return;}else if(clock.ElapsedMilliseconds>15000)throw new InvalidOperationException("dEPTH did not start.");Thread.Sleep(250);}}
}

sealed class LoopbackCapture : IDisposable {
 const uint Loopback=0x00020000,Silent=0x2;readonly string path;readonly ManualResetEvent ready=new ManualResetEvent(false);Thread thread;volatile bool stopping;Exception failure;
 public LoopbackCapture(string output){path=output;}
 public void Start(){thread=new Thread(Run){IsBackground=true,Name="dEPTH system audio recorder"};thread.Start();if(!ready.WaitOne(8000))throw new TimeoutException("System-audio recording did not start.");if(failure!=null)throw new InvalidOperationException("System audio could not be recorded: "+failure.Message,failure);}
 public void Stop(){stopping=true;if(thread!=null&&!thread.Join(5000))throw new TimeoutException("System-audio recording did not stop.");if(failure!=null)throw new InvalidOperationException("System audio recording failed: "+failure.Message,failure);}
 void Run(){IMMDeviceEnumerator enumerator=null;IMMDevice device=null;IAudioClient client=null;IAudioCaptureClient capture=null;IntPtr format=IntPtr.Zero;FileStream stream=null;BinaryWriter writer=null;long riffSize=0,dataSize=0,dataStart=0;
  try{CoInitializeEx(IntPtr.Zero,0);enumerator=(IMMDeviceEnumerator)new MMDeviceEnumerator();Check(enumerator.GetDefaultAudioEndpoint(0,1,out device));Guid audioClient=typeof(IAudioClient).GUID;object activated;Check(device.Activate(ref audioClient,23,IntPtr.Zero,out activated));client=(IAudioClient)activated;Check(client.GetMixFormat(out format));uint sampleRate=(uint)Marshal.ReadInt32(format,4);ushort blockAlign=(ushort)Marshal.ReadInt16(format,12),extra=(ushort)Marshal.ReadInt16(format,16);int formatBytes=18+extra;Check(client.Initialize(0,Loopback,10000000,0,format,IntPtr.Zero));Guid captureId=typeof(IAudioCaptureClient).GUID;object service;Check(client.GetService(ref captureId,out service));capture=(IAudioCaptureClient)service;
   stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read);writer=new BinaryWriter(stream);writer.Write(Encoding.ASCII.GetBytes("RIFF"));riffSize=stream.Position;writer.Write((uint)0);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write((uint)formatBytes);byte[] fmt=new byte[formatBytes];Marshal.Copy(format,fmt,0,fmt.Length);writer.Write(fmt);if((formatBytes&1)!=0)writer.Write((byte)0);writer.Write(Encoding.ASCII.GetBytes("data"));dataSize=stream.Position;writer.Write((uint)0);dataStart=stream.Position;
   Check(client.Start());var audioClock=Stopwatch.StartNew();ready.Set();byte[] managed=null;ulong framesWritten=0;
   while(!stopping){uint packet;Check(capture.GetNextPacketSize(out packet));if(packet==0){Thread.Sleep(3);continue;}while(packet>0){IntPtr data;uint frames,flags;ulong devicePosition,qpc;Check(capture.GetBuffer(out data,out frames,out flags,out devicePosition,out qpc));ulong elapsedFrames=(ulong)(audioClock.Elapsed.TotalSeconds*sampleRate),expectedBefore=elapsedFrames>frames?elapsedFrames-frames:0,gap=expectedBefore>framesWritten?expectedBefore-framesWritten:0;WriteSilence(writer,ref managed,gap,blockAlign);framesWritten+=gap;int bytes=checked((int)frames*blockAlign);if(managed==null||managed.Length<bytes)managed=new byte[bytes];if((flags&Silent)!=0)Array.Clear(managed,0,bytes);else Marshal.Copy(data,managed,0,bytes);writer.Write(managed,0,bytes);framesWritten+=frames;Check(capture.ReleaseBuffer(frames));Check(capture.GetNextPacketSize(out packet));}}
   ulong expectedEnd=(ulong)(audioClock.Elapsed.TotalSeconds*sampleRate);WriteSilence(writer,ref managed,expectedEnd>framesWritten?expectedEnd-framesWritten:0,blockAlign);Check(client.Stop());writer.Flush();long end=stream.Position;stream.Position=riffSize;writer.Write((uint)(end-8));stream.Position=dataSize;writer.Write((uint)(end-dataStart));writer.Flush();
  }catch(Exception ex){failure=ex;ready.Set();}finally{if(format!=IntPtr.Zero)CoTaskMemFree(format);if(writer!=null)writer.Dispose();else if(stream!=null)stream.Dispose();Release(capture);Release(client);Release(device);Release(enumerator);CoUninitialize();}
 }
 static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
 static void WriteSilence(BinaryWriter writer,ref byte[] buffer,ulong frames,ushort blockAlign){if(frames==0)return;int capacity=Math.Max(blockAlign,blockAlign*4096);if(buffer==null||buffer.Length<capacity)buffer=new byte[capacity];Array.Clear(buffer,0,capacity);while(frames>0){int batch=(int)Math.Min(4096,frames);writer.Write(buffer,0,batch*blockAlign);frames-=(uint)batch;}}
 static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 public void Dispose(){ready.Dispose();}
 [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint mode);
 [DllImport("ole32.dll")]static extern void CoUninitialize();
 [DllImport("ole32.dll")]static extern void CoTaskMemFree(IntPtr memory);
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]class MMDeviceEnumerator {}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDeviceEnumerator {int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice endpoint);int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);int RegisterEndpointNotificationCallback(IntPtr client);int UnregisterEndpointNotificationCallback(IntPtr client);}
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDevice {int Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object instance);int OpenPropertyStore(uint access,out IntPtr properties);int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);int GetState(out uint state);}
 [ComImport,Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioClient {int Initialize(int share,uint flags,long duration,long periodicity,IntPtr format,IntPtr session);int GetBufferSize(out uint frames);int GetStreamLatency(out long latency);int GetCurrentPadding(out uint frames);int IsFormatSupported(int share,IntPtr format,out IntPtr closest);int GetMixFormat(out IntPtr format);int GetDevicePeriod(out long normal,out long minimum);int Start();int Stop();int Reset();int SetEventHandle(IntPtr handle);int GetService(ref Guid id,[MarshalAs(UnmanagedType.IUnknown)]out object service);}
 [ComImport,Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioCaptureClient {int GetBuffer(out IntPtr data,out uint frames,out uint flags,out ulong devicePosition,out ulong qpcPosition);int ReleaseBuffer(uint frames);int GetNextPacketSize(out uint frames);}
}
