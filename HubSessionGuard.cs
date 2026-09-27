using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// Optional Windows logon helper. Hub 1.5.1's live settings pipe requires elevation.
// Run independently of dEPTH so an emulator return/restart or crash cannot leave
// ordinary desktop video in automatic SBS mode. Never restart the Hub service.
public static class HubSessionGuard {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"dEPTH","HubSession");
 static readonly string Settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\LocalLow\Samsung\Odyssey3DHubUI\SettingsData\SettingsInfo.json");
 static readonly string[] Fields={"CurrOverlayKey","Curr3DModeKey","Curr3DDepthNegKey","Curr3DDepthPosKey","Curr3DPopoutNegKey","Curr3DPopoutPosKey","Language","CursorType","IsConvertVideoTo3D","IsAutoGameScan","IsAutoRun","IsAutoConvert","IsStandardVideo"};
 static string BaselinePath {get{return Path.Combine(Folder,"baseline.json");}}
 static void Log(string message){string line=DateTime.Now.ToString("s")+" "+message+Environment.NewLine;try{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HubSessionStatus.txt"),line);}catch{}try{Directory.CreateDirectory(Folder);string p=Path.Combine(Folder,"guard.log");if(File.Exists(p)&&new FileInfo(p).Length>262144)File.Move(p,p+"."+DateTime.UtcNow.Ticks);File.AppendAllText(p,line);}catch{}}
 public static string Message(Dictionary<string,object> s,bool active,int standard,int convert){
  return Message(s,active?1:0,standard,convert);
 }
 // 0: normal desktop popup, 1: dEPTH SBS conversion, 2: native
 // SteamVR/Leia output, 3: raw SBS recording with Hub conversion disabled.
 public static string Message(Dictionary<string,object> s,int mode,int standard,int convert){
  bool active=mode==1;
  var values=new List<string>();
  foreach(string field in Fields){
   object v;if(!s.TryGetValue(field,out v))throw new InvalidDataException("Hub setting missing: "+field);
   string value=field=="IsAutoConvert"?(active?"1":"0"):field=="IsStandardVideo"?(active?"0":standard.ToString()):field=="IsConvertVideoTo3D"?(mode==2||mode==3?"0":active?"1":convert.ToString()):Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture);
   if(value.IndexOfAny(new[]{'/','\r','\n'})>=0)throw new InvalidDataException("Unexpected Hub setting format: "+field);
   values.Add(value);
  }
  return "OH/4/11/"+string.Join("/",values);
 }
 static Dictionary<string,object> ReadSettings(){return (Dictionary<string,object>)Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Settings))["Settings"];}
 static bool Apply(int mode){
  try{
   var s=ReadSettings();int standard=Convert.ToInt32(s["IsStandardVideo"]),convert=Convert.ToInt32(s["IsConvertVideoTo3D"]);
   if(File.Exists(BaselinePath)){var baseline=Json.Deserialize<int[]>(File.ReadAllText(BaselinePath));standard=baseline[0];convert=baseline[1];}
   else if(mode!=0){Directory.CreateDirectory(Folder);File.WriteAllText(BaselinePath,Json.Serialize(new[]{standard,convert}));}
   using(var pipe=new NamedPipeClientStream(".","Odyssey3DHubPipe",PipeDirection.InOut)){
    pipe.Connect(1200);
    using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true)){writer.AutoFlush=true;writer.WriteLine(Message(s,mode,standard,convert));}
    // Keep the client alive until Hub has processed and serialized the setting.
    for(int i=0;i<30;i++){
     Thread.Sleep(100);var check=ReadSettings();
     if(Convert.ToInt32(check["IsAutoConvert"])==(mode==1?1:0)&&Convert.ToInt32(check["IsStandardVideo"])==(mode==1?0:standard)&&Convert.ToInt32(check["IsConvertVideoTo3D"])==(mode==2||mode==3?0:mode==1?1:convert)){
      if(mode==2||mode==3)StopConversionPlayer();
      if(mode==0&&File.Exists(BaselinePath))File.Delete(BaselinePath);
      Log(mode==3?"dEPTH recording: raw SBS exposed; Hub conversion suspended.":mode==2?"Native 3D game: Hub video conversion suspended.":mode==1?"dEPTH running: automatic SBS enabled; popup suppressed.":"dEPTH stopped: automatic conversion disabled; popup restored.");return true;
     }
    }
   }
   throw new IOException("Hub did not confirm its settings change.");
  }catch(Exception e){Log("Setting update pending: "+e.Message);return false;}
 }
 static void StopConversionPlayer(){
  // Turning off detection does not stop an already active library capture.
  // Release its Leia session before the launcher starts the native compositor.
  string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Odyssey 3D Hub\Conversion\Odyssey3DPlayer.exe");
  bool closed=false;
  foreach(var p in Process.GetProcessesByName("Odyssey3DPlayer"))using(p){
   try{
    if(!string.Equals(p.MainModule.FileName,expected,StringComparison.OrdinalIgnoreCase))continue;
    p.CloseMainWindow();
    if(!p.WaitForExit(1000)){
     // This capture-only player can ignore WM_CLOSE. Stop only the verified
     // Samsung player, never the game, Hub service, or an unrelated process.
     p.Kill();
     if(!p.WaitForExit(2000))throw new IOException("Hub conversion player has not closed; native launch is delayed.");
    }
    closed=true;
   }catch(InvalidOperationException){}
  }
  if(closed){Log("Previous Hub conversion player closed before native launch.");Thread.Sleep(1000);}
 }
 public static bool Running(IEnumerable<string> paths){
  var allowed=new HashSet<string>(paths.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);
  foreach(var p in Process.GetProcessesByName("Depth"))using(p){try{if(allowed.Contains(p.MainModule.FileName))return true;}catch{}}
  return false;
 }
 static string HubIdentity(){return string.Join(",",Process.GetProcessesByName("Odyssey3DHubService").Select(p=>{using(p){try{return p.Id+":"+p.StartTime.Ticks;}catch{return p.Id.ToString();}}}).OrderBy(s=>s));}
 static bool NativeRequested(){try{using(var e=EventWaitHandle.OpenExisting(@"Local\dEPTH.NativeStereoRequested"))return e.WaitOne(0);}catch(WaitHandleCannotBeOpenedException){return false;}}
 static bool RawRecordingRequested(){try{using(var e=EventWaitHandle.OpenExisting(@"Local\dEPTH.RawSbsRecordingRequested"))return e.WaitOne(0);}catch(WaitHandleCannotBeOpenedException){return false;}}
 static bool NativeGameRunning(){foreach(var p in Process.GetProcessesByName("re9"))using(p){try{if(p.MainModule.FileName.EndsWith(@"\RESIDENT EVIL requiem BIOHAZARD requiem\re9.exe",StringComparison.OrdinalIgnoreCase))return true;}catch{}}return false;}
 static bool NativeLauncherRunning(){foreach(var p in Process.GetProcessesByName("Re9Launcher"))using(p){try{if(string.Equals(p.MainModule.FileName,@"C:\Games\dEPTH\tools\Re9Launcher.exe",StringComparison.OrdinalIgnoreCase))return true;}catch{}}return false;}
 public static int Mode(bool depth,bool request,bool native){return Mode(depth,request,native,false);}
 public static int Mode(bool depth,bool request,bool native,bool raw){return raw?3:request||native?2:depth?1:0;}
 public static void Main(string[] args){
  bool created;using(var mutex=new Mutex(true,@"Local\dEPTH.HubSessionGuard",out created)){
   if(!created)return;
   var security=new System.Security.AccessControl.EventWaitHandleSecurity();
   security.AddAccessRule(new System.Security.AccessControl.EventWaitHandleAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().User,System.Security.AccessControl.EventWaitHandleRights.FullControl,System.Security.AccessControl.AccessControlType.Allow));
   bool readyCreated;
   using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\dEPTH.HubAutomaticReady",out readyCreated,security)){
   using(var nativeReady=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\dEPTH.HubNativeReady",out readyCreated,security)){
   using(var rawReady=new EventWaitHandle(false,EventResetMode.ManualReset,@"Local\dEPTH.HubRawSbsReady",out readyCreated,security)){
   try{
    var paths=Json.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HubSessionPaths.json")));
    int? applied=null;string hub="";DateTime absent=DateTime.UtcNow,nextTry=DateTime.MinValue,nativeSeen=DateTime.MinValue;
    Log("Session guard started. Settings: "+Settings+"; log folder: "+Folder);
    while(true){
     bool active=Running(paths);if(active)absent=DateTime.UtcNow;
     bool native=NativeGameRunning();if(native)nativeSeen=DateTime.UtcNow;
     int mode=Mode(active,(active||NativeLauncherRunning())&&NativeRequested(),native||(DateTime.UtcNow-nativeSeen).TotalSeconds<4,RawRecordingRequested());
     // Internal replacements overlap; this grace also covers a short launch gap.
     if(mode==0&&applied==1&&(DateTime.UtcNow-absent).TotalMilliseconds<750){Thread.Sleep(100);continue;}
     string currentHub=HubIdentity();
     if(currentHub!=hub){ready.Reset();nativeReady.Reset();hub=currentHub;applied=null;nextTry=DateTime.MinValue;}
     if(mode!=1)ready.Reset();if(mode!=2)nativeReady.Reset();if(mode!=3)rawReady.Reset();
     if(mode!=0&&applied==mode){try{var settings=ReadSettings();if(Convert.ToInt32(settings["IsAutoConvert"])!=(mode==1?1:0)||Convert.ToInt32(settings["IsConvertVideoTo3D"])!=(mode==1?1:0)||(mode==1&&Convert.ToInt32(settings["IsStandardVideo"])!=0)){ready.Reset();nativeReady.Reset();applied=null;}}catch{ready.Reset();nativeReady.Reset();applied=null;}}
     if(hub!=""&&(applied!=mode)&&DateTime.UtcNow>=nextTry){if(Apply(mode)){applied=mode;if(mode==1)ready.Set();if(mode==2)nativeReady.Set();if(mode==3)rawReady.Set();}else nextTry=DateTime.UtcNow.AddSeconds(3);}
     Thread.Sleep(200);
    }
   }catch(Exception e){Log("Guard stopped: "+e);Environment.ExitCode=1;}
   }
   }
   }
  }
 }
}
