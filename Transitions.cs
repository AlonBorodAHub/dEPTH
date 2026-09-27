using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// A neutral frame looks the same before and after Samsung enables its lenses.
sealed class TransitionCurtain : Form {
 protected override bool ShowWithoutActivation { get { return true; } }
 protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p; } }
 public TransitionCurtain(Rectangle area) {FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Bounds=area;BackColor=Color.Black;TopMost=true;}
}
public class LibraryResumeState {
 public string Filter="Library",Query="",SelectedKey="";
 public int Page,ParentProcess;
 public float[] GyroBias;
 public string CoverReadyEvent="";
 public Rectangle Screen,SecondaryScreen;
}

public partial class DepthWindow {
 internal static bool RawSbsRecording;
 Timer handoffTimer;
 TransitionCurtain curtain,secondaryReturnCurtain;
 Game activeGame;
 Process launchedProcess;
 IntPtr gameWindow;
 string steamDirectory;
 Stopwatch handoffClock=new Stopwatch();
 long readySince=-1,returnSince=-1;
 bool revealing,playing;
 IntPtr gameVisibilityHook;WinEventCallback gameVisibilityCallback;
 delegate void WinEventCallback(IntPtr hook,uint eventType,IntPtr window,int objectId,int childId,uint thread,uint time);
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventCallback callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
 void WatchGameVisibility(){
  StopWatchingGameVisibility();
  gameVisibilityCallback=(hook,kind,window,obj,child,thread,time)=>{
   if(activeGame==null||!playing||window!=gameWindow||obj!=0||(kind!=0x8001&&kind!=0x8003))return;
   // Cover native emulator exits as soon as their render window disappears,
   // before waiting for the process and its 3D conversion to finish closing.
   libraryMasked=true;CoverSecondaryScreen();CoverScreen();Invalidate();Update();
  };
  gameVisibilityHook=SetWinEventHook(0x8001,0x8003,IntPtr.Zero,gameVisibilityCallback,0,0,0);
 }
 void StopWatchingGameVisibility(){if(gameVisibilityHook!=IntPtr.Zero){UnhookWinEvent(gameVisibilityHook);gameVisibilityHook=IntPtr.Zero;}gameVisibilityCallback=null;}
 bool nativeStereo;
 System.Threading.EventWaitHandle nativeRequest;
 static bool UsesNativeStereo(Game game){
  if(game.SteamId!="3764200")return false;
  string directory=SteamDirectory(game.SteamId);
  return directory!=""&&File.Exists(Path.Combine(directory,"dinput8.dll"))&&File.Exists(Path.Combine(directory,"openvr_api.dll"));
 }
 static bool NativeConversionEvidence(bool compositor,bool lens,bool hubPlayer){return compositor&&lens&&!hubPlayer;}
 void ReleaseNativeStereo(){if(nativeRequest!=null){nativeRequest.Reset();nativeRequest.Dispose();nativeRequest=null;}nativeStereo=false;}
 void PrepareNativeStereo(Game game){
  nativeStereo=UsesNativeStereo(game);if(!nativeStereo)return;
  nativeRequest=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset,@"Local\dEPTH.NativeStereoRequested");
  nativeRequest.Set();
  try{using(var ready=System.Threading.EventWaitHandle.OpenExisting(@"Local\dEPTH.HubNativeReady")){if(!ready.WaitOne(8000))throw new TimeoutException("Hub did not release video conversion for the native 3D game.");}}
  catch{ReleaseNativeStereo();throw;}
 }
 bool NativeOutputReady(){
  bool found=false;
  EnumWindows((window,state)=>{if(!FullscreenGame(window))return true;uint pid;GetWindowThreadProcessId(window,out pid);try{using(var p=Process.GetProcessById((int)pid))if(p.ProcessName=="vrcompositor"&&p.MainModule.FileName.EndsWith(@"\SteamVR\bin\win64\vrcompositor.exe",StringComparison.OrdinalIgnoreCase)){found=true;return false;}}catch{}return true;},IntPtr.Zero);
  // Leia in the compositor activates the lenses itself. A Hub player would
  // weave this image a second time, so it must never satisfy this gate.
  HubReady(IntPtr.Zero); // Refresh only the new log tail for this launch.
  bool lens=hubTail.LastIndexOf("LensOn!",StringComparison.Ordinal)>hubTail.LastIndexOf("LensOff!",StringComparison.Ordinal);
  return StableConversion(NativeConversionEvidence(found,lens,ConversionWindowVisible()),handoffClock.ElapsedMilliseconds,ref hubStableSince);
 }
 long lastHubRead=-1000;string hubTail="";long hubEvidenceOffset,hubStableSince=-1;
 static string HubLogPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@"AppData\LocalLow\Samsung\Odyssey3DHubUI\Service.log");}}
 void ResetHubEvidence(){try{hubEvidenceOffset=new FileInfo(HubLogPath).Length;}catch{hubEvidenceOffset=0;}hubStableSince=-1;lastHubRead=-1000;hubTail="";}
 bool ConversionWindowVisible(){bool found=false;Rectangle screen=Screen.FromControl(this).Bounds;EnumWindows((window,state)=>{if(!IsWindowVisible(window)||IsIconic(window))return true;WindowRect r;if(!GetWindowRect(window,out r)||r.Left>screen.Left+8||r.Top>screen.Top+8||r.Right<screen.Right-8||r.Bottom<screen.Bottom-8)return true;uint pid;GetWindowThreadProcessId(window,out pid);try{using(var process=Process.GetProcessById((int)pid))if(process.ProcessName=="Odyssey3DPlayer"){found=true;return false;}}catch{}return true;},IntPtr.Zero);return found;}
 bool ConversionReady(IntPtr target){return StableConversion(HubReady(target)&&ConversionWindowVisible(),handoffClock.ElapsedMilliseconds,ref hubStableSince);}
 static bool StableConversion(bool confirmed,long now,ref long since){if(!confirmed){since=-1;return false;}if(since<0)since=now;return now-since>=1000;}
 bool LibraryConversionReady(){return RawSbsRecording||ConversionReady(Handle);}

 Process closingProcess;long closingSince=-1;
 bool forcedGameExit;
 ControllerInput controllerInput=new ControllerInput();
 PassiveExitInput passiveExit;
 void StopPassiveExit(){if(passiveExit!=null){passiveExit.Dispose();passiveExit=null;}}
 bool libraryMasked;
 bool resumeLibrary;
 Rectangle returnScreen;long fullscreenRefreshAt=-1;bool returnRefreshRetried;
 delegate bool WindowVisitor(IntPtr window,IntPtr state);
 [StructLayout(LayoutKind.Sequential)] struct WindowRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor,IntPtr state);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out WindowRect rect);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);

 void InitializeTransitions(){handoffTimer=new Timer{Interval=50};handoffTimer.Tick+=(s,e)=>TickHandoff();}
 void CoverScreen(){if(curtain==null||curtain.IsDisposed){curtain=new TransitionCurtain(Screen.FromControl(this).Bounds);curtain.Show();}curtain.Opacity=1;curtain.Refresh();}
 void CoverSecondaryScreen(){
  if(secondaryReturnCurtain!=null&&!secondaryReturnCurtain.IsDisposed)return;
  var displays=Screen.AllScreens.Select(s=>s.Bounds).ToArray();int target=LowerDisplay(Screen.FromControl(this).Bounds,displays);if(target<0)return;
  secondaryReturnCurtain=new TransitionCurtain(displays[target]);secondaryReturnCurtain.Show();secondaryReturnCurtain.Refresh();companionRevealSince=-1;
 }
 void RemoveSecondaryCurtain(){if(secondaryReturnCurtain!=null){secondaryReturnCurtain.Close();secondaryReturnCurtain.Dispose();secondaryReturnCurtain=null;}}
 void RemoveCurtain(){if(curtain!=null){curtain.Close();curtain.Dispose();curtain=null;}}
 void DisposeTransitions(){StopPassiveExit();StopWatchingGameVisibility();ReleaseNativeStereo();ReleaseStereoKey();if(navigationTimer!=null)navigationTimer.Dispose();controllerInput.Dispose();if(handoffTimer!=null)handoffTimer.Dispose();RemoveCurtain();RemoveSecondaryCurtain();if(closingProcess!=null)closingProcess.Dispose();if(launchedProcess!=null){launchedProcess.Dispose();launchedProcess=null;}}
 void PrepareStartup(){PrepareMenuAudio();ResetHubEvidence();startupOpening=!resumeLibrary;libraryMasked=true;Shown+=(s,e)=>{try{WindowRect r;GetWindowRect(Handle,out r);File.WriteAllText(Path.Combine(Storage.Folder,"startup-window.txt"),"Handle="+Handle+"; Bounds="+Bounds+"; Native="+r.Left+","+r.Top+","+r.Right+","+r.Bottom+"; Primary="+Screen.PrimaryScreen.Bounds+"; Current="+Screen.FromControl(this).Bounds+"; Focused="+ContainsFocus);}catch{}handoffClock.Restart();returnSince=0;BringToFront();Activate();SetForegroundWindow(Handle);RefreshFullscreenDetection();handoffTimer.Start();};}
 void RestoreLibrary(LibraryResumeState state){
  filter=state.Filter;query=state.Query;page=state.Page;RefreshGames();
 selected=shown.FirstOrDefault(game=>game.Key==state.SelectedKey)??selected;
  controllerInput.RestoreMotionBias(state.GyroBias);
  if(state.Screen.Width>0&&state.Screen.Height>0){StartPosition=FormStartPosition.Manual;Bounds=state.Screen;}
  resumeLibrary=true;
 }
 void RestartLibraryDisplay(){
  var state=new LibraryResumeState{Filter=filter,Query=query,Page=page,SelectedKey=selected==null?"":selected.Key,Screen=Screen.FromControl(this).Bounds,ParentProcess=Process.GetCurrentProcess().Id,GyroBias=controllerInput.RememberedMotionBias};
  state.SecondaryScreen=secondaryReturnCurtain==null?Rectangle.Empty:secondaryReturnCurtain.Bounds;
  Storage.Save(lib);
  state.CoverReadyEvent="Local\\DepthCover-"+Guid.NewGuid().ToString("N");
  using(var ready=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset,state.CoverReadyEvent)){
   string encoded=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Storage.Json.Serialize(state)));
   string arguments="--resume-library "+encoded+(RawSbsRecording?" --raw-sbs-recording":"");
   using(var replacement=Process.Start(new ProcessStartInfo(Application.ExecutablePath,arguments){WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,UseShellExecute=false}))AllowSetForegroundWindow((uint)replacement.Id);
   // Keep our black curtain alive until the child has painted its own cover.
   if(!ready.WaitOne(5000))throw new TimeoutException("The replacement display did not become ready.");
   try{File.WriteAllText(Path.Combine(Storage.Folder,"return-display.txt"),"Replacement black cover painted before old process exit; selection="+state.SelectedKey);}catch{}
  }
  Close();
 }
 void CheckController(){if(activeGame!=null&&closingSince<0&&controllerInput.Poll())RequestExit();}
 [DllImport("dwmapi.dll")] static extern int DwmFlush();
 void RequestExit(){
  if(activeGame==null||closingSince>=0)return;
  ReleaseStereoKey();CoverSecondaryScreen();CoverScreen();DwmFlush();
  try{
   uint pid=0;if(gameWindow!=IntPtr.Zero&&IsWindow(gameWindow)&&BelongsToGame(gameWindow))GetWindowThreadProcessId(gameWindow,out pid);
   if(pid!=0)closingProcess=Process.GetProcessById((int)pid);
   else if(launchedProcess!=null&&!launchedProcess.HasExited&&activeGame.SteamId=="")closingProcess=Process.GetProcessById(launchedProcess.Id);
   if(closingProcess==null){ReturnFromGame();return;}
   forcedGameExit=false;closingProcess.CloseMainWindow();closingSince=handoffClock.ElapsedMilliseconds;handoffTimer.Start();
  }catch(InvalidOperationException){FinishExit();}
   catch(Exception ex){ExitFailed(ex);}
 }
 void FinishExit(){try{File.WriteAllText(Path.Combine(Storage.Folder,"game-exit.txt"),(activeGame==null?"Game":activeGame.Title)+"; forced="+forcedGameExit+"; elapsed="+(handoffClock.ElapsedMilliseconds-closingSince)+"ms");}catch{}if(closingProcess!=null){closingProcess.Dispose();closingProcess=null;}closingSince=-1;ReturnFromGame();}
 void ExitFailed(Exception ex){if(closingProcess!=null){closingProcess.Dispose();closingProcess=null;}closingSince=-1;ReturnFromGame();MessageBox.Show(this,"Could not close the game: "+ex.Message,"Exit");}

 static string SteamDirectory(string id){
  string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
  var roots=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase){root};
  string file=Path.Combine(root,"steamapps","libraryfolders.vdf");
  if(File.Exists(file))foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))roots.Add(match.Groups[1].Value.Replace(@"\\",@"\"));
  foreach(string folder in roots){string manifest=Path.Combine(folder,"steamapps","appmanifest_"+id+".acf");if(File.Exists(manifest)){string name=Storage.Field(File.ReadAllText(manifest),"installdir");if(name!="")return Path.GetFullPath(Path.Combine(folder,"steamapps","common",name))+Path.DirectorySeparatorChar;}}
  return "";
 }
 bool BelongsToGame(IntPtr window){
  uint pid;GetWindowThreadProcessId(window,out pid);
  if(pid==(uint)Process.GetCurrentProcess().Id)return false;
  try{using(var process=Process.GetProcessById((int)pid)){
   string path=process.MainModule.FileName;
   return activeGame.SteamId!="" ? steamDirectory!=""&&path.StartsWith(steamDirectory,StringComparison.OrdinalIgnoreCase) : string.Equals(path,activeGame.Executable,StringComparison.OrdinalIgnoreCase);
  }}catch{return false;}
 }
 bool FullscreenGame(IntPtr window){
  if(activeGame!=null&&string.Equals(Path.GetFileNameWithoutExtension(activeGame.Executable),"azahar",StringComparison.OrdinalIgnoreCase)){
   var title=new System.Text.StringBuilder(512);GetWindowText(window,title,title.Capacity);
   if(IsAzaharSecondaryTitle(title.ToString()))return false;
  }
  WindowRect r;if(!IsWindowVisible(window)||IsIconic(window)||!GetWindowRect(window,out r))return false;
  Rectangle screen=Screen.FromControl(this).Bounds;
  return r.Left<=screen.Left+3&&r.Top<=screen.Top+3&&r.Right>=screen.Right-3&&r.Bottom>=screen.Bottom-3;
 }
 void BeginGame(Game game){
  if(activeGame!=null)return;
  ProcessStartInfo info=Storage.StartInfo(game);
  controllerInput.SetMotionEnabled(false);controllerInput.Poll();StopPassiveExit();passiveExit=new PassiveExitInput(Handle); // Close the gyro owner before the emulator opens its sensor.
  if(!full)Fullscreen();
  lastTabletCheck=-1000;tabletWindow=IntPtr.Zero;tabletPhase=0;activeGame=game;if(menuAudio!=null){menuAudio.StopIntro();menuAudio.SetMenu(false);}steamDirectory=game.SteamId==""?"":SteamDirectory(game.SteamId);
  ResetHubEvidence();CoverScreen();libraryMasked=true;Invalidate();Update();WatchGameVisibility(); // Keep the underlying library black for the entire game.
  try{
   if(!RawSbsRecording)PrepareNativeStereo(game);PrepareStereoProfile(game);launchedProcess=Process.Start(info);gameWindow=IntPtr.Zero;readySince=-1;returnSince=-1;revealing=false;playing=false;controllerInput.Reset();
   escapeRegistered=RegisterHotKey(Handle,Hotkey,0x4000,(uint)Keys.Escape);
   handoffClock.Restart();lastHubRead=-1000;hubTail="";handoffTimer.Start();
  }catch{StopPassiveExit();StopWatchingGameVisibility();ReleaseNativeStereo();activeGame=null;libraryMasked=false;Invalidate();RemoveCurtain();throw;}
 }
 void TickHandoff(){

  if(curtain!=null)SetWindowPos(curtain.Handle,new IntPtr(-1),0,0,0,0,0x0013);
  if(closingSince>=0){try{if(closingProcess.HasExited){FinishExit();return;}if(handoffClock.ElapsedMilliseconds-closingSince>=3000){forcedGameExit=true;closingProcess.Kill();} }catch(InvalidOperationException){FinishExit();}catch(Exception ex){ExitFailed(ex);}return;}
  if(returnSince>=0){
   if(fullscreenRefreshAt>=0){
    if(handoffClock.ElapsedMilliseconds<fullscreenRefreshAt)return;
    WindowState=FormWindowState.Normal;Show();BringToFront();Activate();SetForegroundWindow(Handle);Bounds=returnScreen;Invalidate();Update();
    RemoveCurtain();BringToFront();Activate();SetForegroundWindow(Handle);
    fullscreenRefreshAt=-1;lastHubRead=-1000;hubTail="";
   }
   // Keep a neutral frame while the Hub switches back to Depth's window.
   double elapsed=handoffClock.ElapsedMilliseconds-returnSince;
   if(elapsed>=250&&LibraryConversionReady()){libraryMasked=false;if(startupOpening){if(menuAudio!=null)menuAudio.StartIntro();introSince=frameClock.ElapsedMilliseconds+IntroSettle;}Invalidate();RemoveCurtain();handoffTimer.Stop();returnSince=-1;return;}
   // Do not resize the capture target during activation: the Hub can take
   // several seconds to enable its lenses and resizing cancels that work.
   if(elapsed>=30000){handoffTimer.Stop();RemoveCurtain();var choice=MessageBox.Show(this,"Odyssey 3D Hub has not activated 3D for Depth. Retry? Cancel closes Depth.","3D connection",MessageBoxButtons.RetryCancel);if(choice==DialogResult.Retry){ResetHubEvidence();handoffClock.Restart();RefreshFullscreenDetection();handoffTimer.Start();}else{Close();}}return;
  }
  if(activeGame==null)return;
  PlaceTabletScreen();
  if(playing){
   if(!IsWindow(gameWindow)||!IsWindowVisible(gameWindow)){
    CoverSecondaryScreen();CoverScreen();
    if(launchedProcess!=null&&!launchedProcess.HasExited&&(stereoProfile!=null||IsWindow(gameWindow)))return;
    ReturnFromGame();
   }else if(curtain!=null&&(RawSbsRecording||(nativeStereo?NativeOutputReady():ConversionReady(gameWindow)))){RemoveCurtain();RemoveSecondaryCurtain();}
   return;
  }
  IntPtr candidate=IntPtr.Zero;
  EnumWindows((window,state)=>{if((nativeStereo?IsWindowVisible(window):FullscreenGame(window))&&BelongsToGame(window)){candidate=window;return false;}return true;},IntPtr.Zero);
  if(candidate==IntPtr.Zero){readySince=-1;hubStableSince=-1;if(handoffClock.ElapsedMilliseconds>60000){RequestExit();}return;}
  if(candidate!=gameWindow||readySince<0){hubStableSince=-1;gameWindow=candidate;readySince=handoffClock.ElapsedMilliseconds;if(!nativeStereo)SetForegroundWindow(gameWindow);return;}
  if(!revealing&&handoffClock.ElapsedMilliseconds-readySince>=200){
   // Keep Depth directly under the game instead of minimizing it to the taskbar.
   SetWindowPos(Handle,gameWindow,0,0,0,0,0x0013);
   if(!nativeStereo)SetForegroundWindow(gameWindow);revealing=true;
  }
  // Never expose raw SBS because a timer expired. Require the game's own
  // fresh conversion evidence and a stable fullscreen conversion surface.
  if(revealing&&(RawSbsRecording||(nativeStereo?NativeOutputReady():ConversionReady(gameWindow)))){RemoveCurtain();playing=true;}
  else if(handoffClock.ElapsedMilliseconds-readySince>=60000){RequestExit();}
 }
 bool HubReady(IntPtr target){
  long now=handoffClock.ElapsedMilliseconds;
  if(now-lastHubRead>=150){lastHubRead=now;try{
   string path=HubLogPath;
   using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    if(file.Length<hubEvidenceOffset)hubEvidenceOffset=0;file.Seek(Math.Max(hubEvidenceOffset,file.Length-16384),SeekOrigin.Begin);using(var reader=new StreamReader(file))hubTail=reader.ReadToEnd();
   }
  }catch{hubTail="";}}
  int start=hubTail.LastIndexOf("Conversion Player started with arguments - Window: ",StringComparison.Ordinal);
  if(start<0)return false;
  string current=hubTail.Substring(start);
  if(!current.StartsWith("Conversion Player started with arguments - Window: "+target.ToInt64()+" | isSideBySide: true",StringComparison.Ordinal))return false;
  return current.LastIndexOf("LensOn!",StringComparison.Ordinal)>current.LastIndexOf("LensOff!",StringComparison.Ordinal);
 }
 void ReturnFromGame(){
  if(restoring)return;restoring=true;
  try{
   string emulator=activeGame==null?"":Path.GetFileNameWithoutExtension(activeGame.Executable);
   bool restartDisplay=nativeStereo||string.Equals(emulator,"azahar",StringComparison.OrdinalIgnoreCase)||string.Equals(emulator,"Dolphin",StringComparison.OrdinalIgnoreCase);
   StopPassiveExit();StopWatchingGameVisibility();libraryMasked=true;Invalidate();Update();ReleaseNativeStereo();
   CoverSecondaryScreen();CoverScreen();try{CaptureStereoProfile();}catch(Exception ex){File.WriteAllText(Path.Combine(Storage.Folder,"stereo-save-error.txt"),ex.ToString());}activeGame=null;playing=false;revealing=false;gameWindow=IntPtr.Zero;
   if(escapeRegistered){UnregisterHotKey(Handle,Hotkey);escapeRegistered=false;}
   if(launchedProcess!=null){launchedProcess.Dispose();launchedProcess=null;}
   controllerInput.Dispose();nextDolphinCheck=0;gyroPointerActive=false;
   // Hub does not reliably reattach conversion to a process it previously left.
   // Start a fresh presentation session, preserving navigation and preferences.
   if(restartDisplay){try{RestartLibraryDisplay();return;}catch(Exception ex){try{File.WriteAllText(Path.Combine(Storage.Folder,"return-display.txt"),"Display restart failed: "+ex.Message);}catch{}}}
   ResetHubEvidence();libraryMasked=true;WindowState=FormWindowState.Normal;Show();Invalidate();Update();
   handoffClock.Restart();lastHubRead=-1000;hubTail="";returnSince=0;returnRefreshRetried=false;RefreshFullscreenDetection();handoffTimer.Start();
  }finally{restoring=false;}
 }
 void RefreshFullscreenDetection(){
  returnScreen=Screen.FromControl(this).Bounds;CoverScreen();
  // Re-enter fullscreen while continuing to present black frames for Hub.
  Bounds=new Rectangle(returnScreen.X+32,returnScreen.Y+32,returnScreen.Width-64,returnScreen.Height-64);
  fullscreenRefreshAt=handoffClock.ElapsedMilliseconds+300;
 }
}


