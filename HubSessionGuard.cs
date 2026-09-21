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
  var values=new List<string>();
  foreach(string field in Fields){
   object v;if(!s.TryGetValue(field,out v))throw new InvalidDataException("Hub setting missing: "+field);
   string value=field=="IsAutoConvert"?(active?"1":"0"):field=="IsStandardVideo"?(active?"0":standard.ToString()):field=="IsConvertVideoTo3D"?(active?"1":convert.ToString()):Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture);
   if(value.IndexOfAny(new[]{'/','\r','\n'})>=0)throw new InvalidDataException("Unexpected Hub setting format: "+field);
   values.Add(value);
  }
  return "OH/4/11/"+string.Join("/",values);
 }
 static Dictionary<string,object> ReadSettings(){return (Dictionary<string,object>)Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Settings))["Settings"];}
 static bool Apply(bool active){
  try{
   var s=ReadSettings();int standard=Convert.ToInt32(s["IsStandardVideo"]),convert=Convert.ToInt32(s["IsConvertVideoTo3D"]);
   if(File.Exists(BaselinePath)){var baseline=Json.Deserialize<int[]>(File.ReadAllText(BaselinePath));standard=baseline[0];convert=baseline[1];}
   else if(active){Directory.CreateDirectory(Folder);File.WriteAllText(BaselinePath,Json.Serialize(new[]{standard,convert}));}
   using(var pipe=new NamedPipeClientStream(".","Odyssey3DHubPipe",PipeDirection.InOut)){
    pipe.Connect(1200);
    using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true)){writer.AutoFlush=true;writer.WriteLine(Message(s,active,standard,convert));}
    // Keep the client alive until Hub has processed and serialized the setting.
    for(int i=0;i<30;i++){
     Thread.Sleep(100);var check=ReadSettings();
     if(Convert.ToInt32(check["IsAutoConvert"])==(active?1:0)&&Convert.ToInt32(check["IsStandardVideo"])==(active?0:standard)&&Convert.ToInt32(check["IsConvertVideoTo3D"])==(active?1:convert)){
      if(!active&&File.Exists(BaselinePath))File.Delete(BaselinePath);
      Log(active?"dEPTH running: automatic SBS enabled; popup suppressed.":"dEPTH stopped: automatic conversion disabled; popup restored.");return true;
     }
    }
   }
   throw new IOException("Hub did not confirm its settings change.");
  }catch(Exception e){Log("Setting update pending: "+e.Message);return false;}
 }
 public static bool Running(IEnumerable<string> paths){
  var allowed=new HashSet<string>(paths.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);
  foreach(var p in Process.GetProcessesByName("Depth"))using(p){try{if(allowed.Contains(p.MainModule.FileName))return true;}catch{}}
  return false;
 }
 static string HubIdentity(){return string.Join(",",Process.GetProcessesByName("Odyssey3DHubService").Select(p=>{using(p){try{return p.Id+":"+p.StartTime.Ticks;}catch{return p.Id.ToString();}}}).OrderBy(s=>s));}
 public static void Main(string[] args){
  bool created;using(var mutex=new Mutex(true,@"Local\dEPTH.HubSessionGuard",out created)){
   if(!created)return;
   try{
    var paths=Json.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HubSessionPaths.json")));
    bool? applied=null;string hub="";DateTime absent=DateTime.UtcNow,nextTry=DateTime.MinValue;
    Log("Session guard started. Settings: "+Settings+"; log folder: "+Folder);
    while(true){
     bool active=Running(paths);if(active)absent=DateTime.UtcNow;
     // Internal replacements overlap; this grace also covers a short launch gap.
     if(!active&&applied==true&&(DateTime.UtcNow-absent).TotalMilliseconds<750){Thread.Sleep(100);continue;}
     string currentHub=HubIdentity();
     if(currentHub!=hub){hub=currentHub;applied=null;nextTry=DateTime.MinValue;}
     if(hub!=""&&(applied!=active)&&DateTime.UtcNow>=nextTry){if(Apply(active))applied=active;else nextTry=DateTime.UtcNow.AddSeconds(3);}
     Thread.Sleep(200);
    }
   }catch(Exception e){Log("Guard stopped: "+e);Environment.ExitCode=1;}
  }
 }
}
