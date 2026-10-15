using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DepthWindow {
 string scanFolder="";System.Threading.CancellationTokenSource scanCancel;string scanOutcome="";
 bool scanBusy,browseFolders;string browsePath="",browseReturn="";int browsePage;Action<string> browseApply;
 List<string> browseEntries=new List<string>();
 void OpenScan(){if(scanBusy){panel="scan";return;}scanFolder=(lib.RomFolders??new List<string>()).LastOrDefault(Directory.Exists)??"";panel="scan";menuIndex=0;panelMessage="Choose a ROM folder, then Scan. Systems, titles, emulators and artwork are automatic.";Invalidate();}
 void AddRoms(){OpenScan();OpenBrowser(scanFolder,true,v=>{scanFolder=v;menuIndex=1;});}
 List<Game> SetupGames(){return lib.Games.Where(g=>g.SteamId==""&&(g.Platform=="Unknown"||!File.Exists(g.Executable))).ToList();}
 int setupPage;
 string[] ScanItems(){if(scanBusy)return new[]{"Scanning…", "Cancel scan"};var items=new List<string>{"ROM folder: "+(scanFolder==""?"Choose folder…":scanFolder),"Scan folder","Scan saved libraries"};int count=SetupGames().Count;if(count>0)items.Add("Review games needing setup ("+count+")");items.Add("Back");return items.ToArray();}
 string[] SetupItems(){return SetupGames().Skip(setupPage*8).Take(8).Select(g=>g.Title+" · "+(g.Platform=="Unknown"?"System not detected":"Emulator not found")).Concat(new[]{"Next page","Back"}).ToArray();}
 void ActivateSetup(){var games=SetupGames();int count=Math.Min(8,Math.Max(0,games.Count-setupPage*8));if(menuIndex<count){Edit(games[setupPage*8+menuIndex]);}else if(menuIndex==count){setupPage=(setupPage+1)%Math.Max(1,(games.Count+7)/8);menuIndex=0;}else{panel="scan";menuIndex=0;}}
 void ScanProgress(string message){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)(()=>{if(IsDisposed)return;panelMessage=message;Invalidate();}));}catch(InvalidOperationException){}}
 void ReloadImportMedia(){gameMedia=null;companionGame=null;companionFrameGame=null;companionBackground=null;foreach(var bitmap in new HashSet<System.Drawing.Bitmap>(companionFrames.Values))bitmap.Dispose();companionFrames.Clear();ClearRenderCache();}
 void OpenBrowser(string path,bool folders,Action<string> apply){
  browseReturn=panel;browseFolders=folders;browseApply=apply;browsePage=0;
  browsePath=Directory.Exists(path)?path:File.Exists(path)?Path.GetDirectoryName(path):Directory.Exists(lib.Root)?lib.Root:Directory.Exists(@"C:\Games\Emulators")?@"C:\Games\Emulators":Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  panel="browse";menuIndex=0;LoadBrowser();
 }
 void LoadBrowser(){
  browseEntries.Clear();try{
   if(browsePath=="")browseEntries=DriveInfo.GetDrives().Where(d=>d.IsReady).Select(d=>d.RootDirectory.FullName).ToList();
   else {browseEntries.AddRange(Directory.GetDirectories(browsePath).OrderBy(x=>x));if(!browseFolders)browseEntries.AddRange(Directory.GetFiles(browsePath).OrderBy(x=>x));}
   panelMessage=browsePath==""?"Choose a drive":browsePath;
  }catch(Exception ex){panelMessage=ex.Message;}
  browsePage=Math.Max(0,Math.Min(browsePage,Math.Max(0,(browseEntries.Count-1)/6)));Invalidate();
 }
 string[] BrowserItems(){return new[]{"Up / Drives",browseFolders?"Use this folder":"Enter file path", "Enter folder path"}.Concat(browseEntries.Skip(browsePage*6).Take(6).Select(p=>(Directory.Exists(p)?"[Folder] ":"")+Path.GetFileName(p.TrimEnd('\\')))).Concat(new[]{"Next page ("+(browsePage+1)+"/"+Math.Max(1,(browseEntries.Count+5)/6)+")","Cancel"}).ToArray();}
 void ChooseBrowser(string value){var apply=browseApply;panel=browseReturn;menuIndex=0;browseApply=null;if(apply!=null)apply(value);Invalidate();}
 void ActivateBrowser(){
  int count=Math.Min(6,browseEntries.Count-browsePage*6);
  if(menuIndex==0){browsePath=browsePath==""?"":Directory.GetParent(browsePath)==null?"":Directory.GetParent(browsePath).FullName;browsePage=0;LoadBrowser();}
  else if(menuIndex==1){if(browseFolders){if(Directory.Exists(browsePath))ChooseBrowser(browsePath);}else OpenKeyboard("File path","",v=>{if(File.Exists(v))ChooseBrowser(v);else{panel="browse";panelMessage="File not found";}});}
  else if(menuIndex==2)OpenKeyboard("Folder path",browsePath,v=>{panel="browse";if(Directory.Exists(v)){browsePath=v;browsePage=0;LoadBrowser();}else panelMessage="Folder not found";});
  else if(menuIndex<3+count){string value=browseEntries[browsePage*6+menuIndex-3];if(Directory.Exists(value)){browsePath=value;browsePage=0;menuIndex=0;LoadBrowser();}else ChooseBrowser(value);}
  else if(menuIndex==3+count){browsePage=(browsePage+1)%Math.Max(1,(browseEntries.Count+5)/6);menuIndex=0;LoadBrowser();}
  else {panel=browseReturn;menuIndex=0;}
 }
 async void ActivateScan(){
  if(scanBusy){if(menuIndex==1){scanCancel.Cancel();panelMessage="Stopping… imported games are saved.";}return;}
  if(menuIndex==0){OpenBrowser(scanFolder,true,v=>{scanFolder=v;menuIndex=1;});return;}
  if(menuIndex>=3){if(menuIndex==3&&SetupGames().Count>0){panel="import-review";setupPage=0;menuIndex=0;}else panel="";return;}
  var folders=new List<string>();bool saved=menuIndex==2;
  if(saved){folders.AddRange(lib.RomFolders??new List<string>());folders.AddRange((lib.Sources??new List<EmulatorSource>()).Select(s=>s.GamesFolder));if(Directory.Exists(lib.Root))folders.Add(lib.Root);folders.AddRange(lib.Games.Where(g=>g.SteamId==""&&File.Exists(g.Rom)).Select(g=>Path.GetDirectoryName(g.Rom)));}
  else if(Directory.Exists(scanFolder))folders.Add(scanFolder);
  if(folders.Count==0){panelMessage="Choose a ROM folder first.";menuIndex=0;return;}
  folders=folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  folders=folders.Where(f=>!folders.Any(parent=>!string.Equals(f,parent,StringComparison.OrdinalIgnoreCase)&&f.StartsWith(parent.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))).ToList();
  scanBusy=true;scanCancel=new System.Threading.CancellationTokenSource();var token=scanCancel.Token;menuIndex=0;panelMessage="Looking for games…";Invalidate();
  var copy=Storage.Json.Deserialize<Library>(Storage.Json.Serialize(lib));
  try{
   int added=0,existing=0,unknown=0,missing=0;ImportMediaResult art=null;
   await Task.Run(()=>{
    try{
     foreach(string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase)){
      token.ThrowIfCancellationRequested();if(!Directory.Exists(folder))continue;
      var result=AutoImport.Scan(copy,folder,ScanProgress,token);added+=result.Added;existing+=result.Existing;unknown+=result.Unknown;missing+=result.MissingEmulator;
      if(copy.RomFolders==null)copy.RomFolders=new List<string>();if(!copy.RomFolders.Contains(folder,StringComparer.OrdinalIgnoreCase))copy.RomFolders.Add(folder);
      Storage.Save(copy);
     }
     art=ImportMedia.Enrich(copy.Games.Where(g=>g.SteamId==""&&folders.Any(folder=>g.Rom.StartsWith(folder.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))),ScanProgress,token);
    }finally{Storage.Save(copy);}
   });
   scanOutcome="Added "+added+" · Existing "+existing+" · Needs setup "+copy.Games.Count(g=>g.SteamId==""&&(g.Platform=="Unknown"||!File.Exists(g.Executable)))+" · Missing media "+(art==null?0:art.Incomplete);
   panelMessage=scanOutcome;
  }catch(OperationCanceledException){panelMessage="Stopped. Imported games and downloaded artwork are saved.";}
  catch(Exception ex){panelMessage="Games saved. Scan could not finish: "+ex.Message;}
  finally{
   if(!IsDisposed){lib=copy;filter="Library";query="";RefreshGames();ReloadImportMedia();scanBusy=false;scanCancel.Dispose();scanCancel=null;menuIndex=1;Invalidate();}
  }
 }
}
