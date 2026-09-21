using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class Game {
 public string Title="", Platform="", Executable="", Arguments="", Rom="", SteamId="", Cover="", Note="";
 public bool Favorite=false;
 public string Key { get { return SteamId!="" ? "steam:"+SteamId : Rom; } }
}
public class Library {
 public List<EmulatorSource> Sources=new List<EmulatorSource>();
 public bool ScanSteam=true;
 public bool EnableStereoProfiles=true;
 public string Root=@"C:\Games\Emulators";
 public List<Game> Games=new List<Game>();
 public List<string> HiddenGameKeys=new List<string>();
 public bool IsGameVisible(Game game){return IsPlatformVisible(game.Platform)&&(HiddenGameKeys==null||!HiddenGameKeys.Contains(game.Key));}
 public List<string> HiddenPlatforms=new List<string>{"Wii U"};
 public bool IsPlatformVisible(string platform){return HiddenPlatforms==null||!HiddenPlatforms.Any(p=>string.Equals(p,platform,StringComparison.OrdinalIgnoreCase));}
 public bool HeadTracking=true;
 public bool Stereo=false, SwapEyes=false, Fullscreen=false;
 public float Depth=1;
}
public static class Storage {
 public static string Folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
 public static string FileName {get{return Path.Combine(Folder,"library.json");}}
 public static JavaScriptSerializer Json=new JavaScriptSerializer();
 public static Library Load() {
  Directory.CreateDirectory(Folder);
  if(!File.Exists(FileName)) return new Library();
  try {var library=Json.Deserialize<Library>(File.ReadAllText(FileName));DiscSystems.Separate(library);return library;}
  catch(Exception e) {throw new InvalidDataException("Could not read library.json. Your library has not been overwritten. "+e.Message);}
 }
 public static void Save(Library l) {
  Directory.CreateDirectory(Folder); string tmp=FileName+".tmp";
  File.WriteAllText(tmp,Json.Serialize(l));
  if(File.Exists(FileName)) File.Replace(tmp,FileName,FileName+".bak"); else File.Move(tmp,FileName);
 }
 public static string Field(string data,string field) {return Regex.Match(data,"\""+field+"\"\\s+\"([^\"]*)\"").Groups[1].Value;}
 public static void Scan(Library l) {
  var found=new List<Game>();
  foreach(var source in l.Sources??new List<EmulatorSource>())found.AddRange(PublicSetup.ScanSource(source));
  if(Directory.Exists(l.Root)) foreach(string dir in Directory.GetDirectories(l.Root)) {
   string platform=Path.GetFileName(dir), roms=Path.Combine(dir,"ROMS");
   if(!l.IsPlatformVisible(platform))continue;
   if(!Directory.Exists(roms))continue;
   string pattern=platform=="3DS"?"citra-qt.exe":DiscSystems.IsDolphin(platform)?"Dolphin.exe":platform=="Dreamcast"?"flycast.exe":platform=="PS2"?"pcsx2*.exe":platform=="PS3"?"rpcs3.exe":platform=="Wii U"?"Cemu.exe":platform=="Switch"?"yuzu.exe":"__none__";
   string exe=Directory.GetFiles(dir,pattern,SearchOption.AllDirectories).OrderBy(x=>x.Length).FirstOrDefault()??""; if(platform=="3DS"&&File.Exists(@"C:\Program Files\Azahar\azahar.exe"))exe=@"C:\Program Files\Azahar\azahar.exe";
   if(exe==""&&DiscSystems.IsDolphin(platform))exe=Directory.GetFiles(l.Root,"Dolphin.exe",SearchOption.AllDirectories).OrderBy(x=>x.Length).FirstOrDefault()??"";
   foreach(string rom in Directory.GetFiles(roms,"*",SearchOption.AllDirectories)) {
    if(!Regex.IsMatch(Path.GetExtension(rom),@"^\.(3ds|cci|cxi|chd|gdi|cdi|iso|rvz|gcm|wbfs|wua|wud|rpx|xci|nsp)$",RegexOptions.IgnoreCase))continue;
    string title=Regex.Replace(Path.GetFileNameWithoutExtension(rom),@"\s*[\(\[].*$","").Trim();
    string args=DiscSystems.IsDolphin(platform)?"-b --config Dolphin.Display.Fullscreen=True -e {rom}":platform=="Dreamcast"?"-config window:fullscreen=yes {rom}":platform=="PS2"?"-fullscreen {rom}":platform=="PS3"?"--no-gui --fullscreen {rom}":platform=="Wii U"?"-f -g {rom}":platform=="Switch"?"-f -g {rom}":"{rom}";
    found.Add(new Game {Title=title,Platform=DiscSystems.IsDolphin(platform)?DiscSystems.Detect(rom,platform):platform,Executable=exe,Rom=rom,Arguments=args,Note="Uses your existing 3D configuration."});
   }
   if(platform=="PS3"&&exe!="") {
    string installed=Path.Combine(Path.GetDirectoryName(exe),"dev_hdd0","game");
    if(Directory.Exists(installed))foreach(string gameDir in Directory.GetDirectories(installed)) {
     string boot=Path.Combine(gameDir,"USRDIR","EBOOT.BIN");if(!File.Exists(boot))continue;
     string sfo=Path.Combine(gameDir,"PARAM.SFO");if(SfoField(sfo,"CATEGORY")!="HG")continue;
     string title=SfoField(sfo,"TITLE");if(title=="")title=Path.GetFileName(gameDir);
     found.Add(new Game{Title=title,Platform="PS3",Executable=exe,Rom=boot,Arguments="--no-gui --fullscreen {rom}",Cover=Path.Combine(gameDir,"ICON0.PNG"),Note="Uses your existing 3D configuration."});
    }
   }
  }
  string steam=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam");
  var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);paths.Add(steam);
  string vdf=Path.Combine(steam,"steamapps","libraryfolders.vdf");
  if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))paths.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
  foreach(string path in l.ScanSteam?paths:new HashSet<string>()) {
   string apps=Path.Combine(path,"steamapps");if(!Directory.Exists(apps))continue;
   foreach(string manifest in Directory.GetFiles(apps,"appmanifest_*.acf")) {
    string data=File.ReadAllText(manifest), id=Field(data,"appid"),title=Field(data,"name");
    if(title==""||id=="228980"||id=="250820")continue;
    found.Add(new Game {Title=title,Platform="Steam",SteamId=id,Note="Launch through Steam with your existing 3D configuration."});
   }
  }
  foreach(Game g in found)if(l.IsGameVisible(g)&&!l.Games.Any(x=>x.Key==g.Key))l.Games.Add(g);
  DiscSystems.Separate(l);
  l.Games=l.Games.OrderBy(x=>x.Platform=="Steam"?1:0).ThenBy(x=>x.Platform).ThenBy(x=>x.Title).ToList();
 }
 public static string SfoField(string path,string wanted) {
  if(!File.Exists(path))return "";
  try {byte[] b=File.ReadAllBytes(path);int keys=BitConverter.ToInt32(b,8),values=BitConverter.ToInt32(b,12),count=BitConverter.ToInt32(b,16);
   for(int i=0;i<count;i++){int p=20+i*16,k=keys+BitConverter.ToUInt16(b,p);int end=Array.IndexOf(b,(byte)0,k);string key=System.Text.Encoding.UTF8.GetString(b,k,end-k);if(key==wanted){int start=values+BitConverter.ToInt32(b,p+12),length=BitConverter.ToInt32(b,p+4);return System.Text.Encoding.UTF8.GetString(b,start,length).TrimEnd('\0').Replace('\n',' ');}}
  }catch{}return "";
 }
 public static ProcessStartInfo StartInfo(Game g) {
  if(g.SteamId!="") {if(!Regex.IsMatch(g.SteamId,@"^\d+$"))throw new Exception("Steam App ID must contain only digits.");return new ProcessStartInfo("steam://rungameid/"+g.SteamId){UseShellExecute=true};}
  if(!File.Exists(g.Executable))throw new Exception("Choose the emulator executable in this game's profile.");
  if(g.Rom!=""&&!File.Exists(g.Rom)&&!Directory.Exists(g.Rom))throw new Exception("The game file no longer exists. Update its profile.");
  if(g.Rom.Contains("\""))throw new Exception("The game path contains an invalid quote.");
  string arguments=(g.Arguments??"").Replace("{rom}","\""+g.Rom+"\"");
  // Exit is already requested explicitly through dEPTH. Avoid Dolphin's hidden
  // confirmation dialog (and Windows notification sound) beneath the curtain.
  if(string.Equals(Path.GetFileNameWithoutExtension(g.Executable),"Dolphin",StringComparison.OrdinalIgnoreCase))arguments="--config Dolphin.Interface.ConfirmStop=False "+arguments;
  return new ProcessStartInfo(g.Executable,arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(g.Executable)};
 }
}

public partial class DepthWindow:Form {
 Library lib; List<Game> shown=new List<Game>();
 IEnumerable<Game> VisibleGames {get{return lib.Games.Where(g=>lib.IsGameVisible(g));}}
 readonly Dictionary<Game,string> coverPaths=new Dictionary<Game,string>();
 Dictionary<string,Image> images=new Dictionary<string,Image>();
 Dictionary<Game,float> hover=new Dictionary<Game,float>();
 Dictionary<Game,RectangleF> hits=new Dictionary<Game,RectangleF>();
 Dictionary<Game,RectangleF> leftHits=new Dictionary<Game,RectangleF>();
 Dictionary<string,RectangleF> buttons=new Dictionary<string,RectangleF>();
 Timer animation=new Timer(); Game selected, over; string filter="Favorites",query="",status=""; int page=0;
 bool full=false, escapeRegistered=false; Rectangle oldBounds; bool restoring=false;
 const int Hotkey=742;
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
 [STAThread] public static void Main(string[] args) {
  SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try {
   if(args.Contains("--self-test")){SelfTest.Run();return;}
   if(args.Contains("--scan")){var library=Storage.Load();Storage.Scan(library);Storage.Save(library);return;}
   if(args.Contains("--setup")||(!File.Exists(Storage.FileName)&&!args.Any(a=>a.StartsWith("--render")))){if(!PublicSetup.Show())return;if(args.Contains("--setup"))return;}
   LibraryResumeState resume=null;TransitionCurtain resumeCover=null,resumeSecondaryCover=null;int resumeArg=Array.IndexOf(args,"--resume-library");
   if(resumeArg>=0&&resumeArg+1<args.Length){
    resume=Storage.Json.Deserialize<LibraryResumeState>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(args[resumeArg+1])));
    // Paint before the old process exits so the desktop is never exposed.
    resumeCover=new TransitionCurtain(resume.Screen);resumeCover.Show();resumeCover.Refresh();Application.DoEvents();
    if(resume.SecondaryScreen.Width>0&&resume.SecondaryScreen.Height>0){resumeSecondaryCover=new TransitionCurtain(resume.SecondaryScreen);resumeSecondaryCover.Show();resumeSecondaryCover.Refresh();}
    if(!string.IsNullOrEmpty(resume.CoverReadyEvent)){using(var ready=System.Threading.EventWaitHandle.OpenExisting(resume.CoverReadyEvent))ready.Set();}
    if(resume.ParentProcess>0){try{using(var previous=Process.GetProcessById(resume.ParentProcess)){if(!previous.WaitForExit(5000)){resumeCover.Close();resumeCover.Dispose();if(resumeSecondaryCover!=null)resumeSecondaryCover.Dispose();return;}}}catch(ArgumentException){}}
   }
   var win=new DepthWindow();if(resume!=null){win.secondaryReturnCurtain=resumeSecondaryCover;win.RestoreLibrary(resume);win.Shown+=(s,e)=>{win.Update();resumeCover.Close();resumeCover.Dispose();win.BringToFront();win.Activate();SetForegroundWindow(win.Handle);};}
   if(args.Contains("--render")||args.Contains("--render-sbs")) {win.ClientSize=new Size(1600,1000);win.CreateControl();if(args.Contains("--menu"))win.panel="menu";if(args.Contains("--keyboard"))win.OpenSearch();bool stereo=win.lib.Stereo;win.lib.Stereo=args.Contains("--render-sbs");if(args.Contains("--hover")){win.over=win.shown.FirstOrDefault();if(win.over!=null)win.hover[win.over]=1;}using(var b=new Bitmap(1600,1000))using(var g=Graphics.FromImage(b)){win.OnPaint(new PaintEventArgs(g,new Rectangle(0,0,1600,1000)));b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,win.lib.Stereo?"preview-sbs.png":"preview.png"));}win.lib.Stereo=stereo;win.Dispose();return;}
   win.PrepareStartup();Application.Run(win);
  }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.log"),e.ToString());if(!args.Contains("--self-test"))MessageBox.Show(e.Message,"Depth");Environment.ExitCode=1;}
 }
 public DepthWindow() {
  using(var stream=typeof(DepthWindow).Assembly.GetManifestResourceStream("Depth.AppIcon"))
  using(var icon=new Icon(stream)) Icon=(Icon)icon.Clone();
  Text="dEPTH | Spatial game library";ClientSize=new Size(1440,940);MinimumSize=new Size(1000,740);StartPosition=FormStartPosition.CenterScreen;
  DoubleBuffered=true;KeyPreview=true;BackColor=Color.FromArgb(9,14,25);
  lib=Storage.Load();if(lib.Games.Count==0){Storage.Scan(lib);Storage.Save(lib);}if(!lib.Games.Any(g=>g.Favorite))filter="Library";RefreshGames();
  lib.Stereo=true;lib.Fullscreen=true;
  Shown+=(s,e)=>{if(!full)Fullscreen();};
  InitializeRendering();InitializeTransitions();InitializeNavigation();InitializeCompanion();
  animation.Interval=8;animation.Tick+=(s,e)=>AnimateFrame();animation.Start();
  MouseMove+=(s,e)=>MovePointer(e.Location);
  MouseLeave+=(s,e)=>{over=null;pointerInside=false;Invalidate();};
  MouseWheel+=(s,e)=>ChangePage(e.Delta<0?1:-1);
  MouseClick+=ClickScene;
  KeyDown+=KeysDown;
  FormClosing+=(s,e)=>{if(escapeRegistered)UnregisterHotKey(Handle,Hotkey);try{Storage.Save(lib);}catch(Exception ex){MessageBox.Show(ex.Message,"Could not save library");}};
 }
 void RefreshGames(){shown=VisibleGames.Where(g=>(filter=="Library"||filter=="Favorites"&&g.Favorite||g.Platform==filter)&&(query==""||g.Title.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).ToList();over=null;page=Math.Max(0,Math.Min(page,Math.Max(0,(shown.Count-1)/10)));if(selected==null||!shown.Contains(selected))selected=shown.FirstOrDefault();Invalidate();}
 void ChangePage(int d){page=Math.Max(0,Math.Min(Math.Max(0,(shown.Count-1)/10),page+d));over=null;Invalidate();}
 PointF Logical(Point p){return StereoMath.Pointer(p,ClientSize);}
 Dictionary<Game,RectangleF> HitMap(Point p){return hits;}
 Color Ink(int a,int r,int g,int b){return Color.FromArgb(a,r,g,b);}
 void TextAt(Graphics g,string text,float size,Color c,float x,float y,float width=1300,FontStyle style=FontStyle.Regular){using(var f=new Font("Segoe UI",size,style,GraphicsUnit.Pixel))using(var b=new SolidBrush(c))using(var fmt=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text,f,b,new RectangleF(x,y,width,size*1.8f),fmt);}
 void Fill(Graphics g,Color c,RectangleF r){using(var b=new SolidBrush(c))g.FillRectangle(b,r);}
 void Line(Graphics g,Color c,float x,float y,float x2,float y2,float width=1){using(var p=new Pen(c,width))g.DrawLine(p,x,y,x2,y2);}
 PointF Project(float x,float y,float z,float eye){float k=900/(900-z);return new PointF(720+x*k+eye*(1-k),470+y*k);}
 RectangleF Plane(float x,float y,float w,float h,float z,float eye){PointF p=Project(x,y,z,eye);float k=900/(900-z);return new RectangleF(p.X,p.Y,w*k,h*k);}
 void Button(Graphics g,string id,string label,RectangleF r,bool active=false){buttons[id]=r;Fill(g,active?Color.FromArgb(29,61,70):Color.FromArgb(20,30,44),r);TextAt(g,label,14,active?Color.FromArgb(136,245,221):Color.FromArgb(170,189,202),r.X+14,r.Y+11,r.Width-20);}
 protected override void OnPaint(PaintEventArgs e){FrameTiming.Frame();long stamp=FrameTiming.Start();try{RenderFast(e.Graphics);}finally{FrameTiming.End("main paint",stamp);}}
 void DrawScene(Graphics g,float eye){if(panel!=""){Fill(g,Color.FromArgb(20,42,57),new RectangleF(0,0,1440,940));DrawControllerPanel(g);return;}
  if(!drawingArrivalChrome)DrawBackdrop(g,eye);
  // Extend past the outer edge so antialiasing cannot expose the bright backdrop.
  Fill(g,Color.FromArgb(210,8,15,25),new RectangleF(-1,-1,1442,103));
  TextAt(g,"dEPTH",25,Color.White,46,31,180,FontStyle.Bold);
  Button(g,"search",query==""?"Search":query,new RectangleF(615,28,270,44));Button(g,"scan","Scan",new RectangleF(904,28,132,44));TextAt(g,"STEREO  3D",14,Color.FromArgb(136,245,221),1066,40,145);Button(g,"settings","Settings",new RectangleF(1205,28,115,44));Button(g,"full","[  ]",new RectangleF(1332,28,62,44));
  string heading=FocusedCover==null?"Library":FocusedCover.Title;
  float headingSize=38;using(var f=new Font("Segoe UI",headingSize,FontStyle.Bold,GraphicsUnit.Pixel)){float measured=g.MeasureString(heading,f).Width;if(measured>1320)headingSize*=1320/measured;}
  TextAt(g,heading,headingSize,Color.FromArgb(247,252,253),49,151+(38-headingSize)/2,1340,FontStyle.Bold);TextAt(g,shown.Count+" games",14,Color.FromArgb(22,61,78),52,205,1200);
  var tabs=new List<string>{"Library","Favorites"};tabs.AddRange(VisibleGames.Select(x=>x.Platform).Distinct());float tx=52;foreach(string tab in tabs){float tw=tab.Length*8.5f+32;Button(g,"tab:"+tab,tab,new RectangleF(tx,246,tw,40),filter==tab);tx+=tw+8;}
  if(shown.Count==0){TextAt(g,"No games",28,Color.White,270,440,950,FontStyle.Bold);}
  Fill(g,Color.FromArgb(240,8,15,25),new RectangleF(0,840,1440,100));Line(g,Color.FromArgb(40,105,150,164),48,840,1392,840);
  TextAt(g,(selected==null?"":selected.Platform+"    ")+"A  Play    B  Back    L/R  Tabs    X  Favorite    Y  Search    +  Menu",12,Color.FromArgb(130,155,172),52,891,890);
  Button(g,"prev","<",new RectangleF(969,866,42,40));TextAt(g,(page+1)+" / "+Math.Max(1,(shown.Count+9)/10),13,Color.White,1027,876,75);Button(g,"next",">",new RectangleF(1101,866,42,40));Button(g,"edit","Edit",new RectangleF(1160,866,119,40));Button(g,"play","Play",new RectangleF(1290,866,105,40),true);
 }
 void DrawBackdropBase(Graphics g){
  using(var bg=new LinearGradientBrush(new Rectangle(0,0,1440,940),Color.FromArgb(102,155,175),Color.FromArgb(38,76,112),70f))g.FillRectangle(bg,0,0,1440,940);
  using(var path=new GraphicsPath()){path.AddEllipse(-250,-400,2000,1600);using(var glow=new PathGradientBrush(path)){glow.CenterColor=Color.FromArgb(130,172,236,235);glow.SurroundColors=new[]{Color.FromArgb(0,60,90,130)};g.FillPath(glow,path);}}
 }
 public static float GridDepth(float x,float y,float curvature){return curvature*(-1250+Math.Min(1190,(x*x+y*y)/4200));}
 void DrawBackdrop(Graphics g,float eye){DrawBackdrop(g,eye,1);}
 void DrawBackdrop(Graphics g,float eye,float curvature){DrawBackdropBase(g);
  // A concave surface: its center lies behind the screen, its edges rise toward it.
  for(int axis=0;axis<2;axis++)for(int n=-20;n<=20;n++){PointF? last=null;for(int step=-40;step<=40;step++){float x=axis==0?n*130:step*65,y=axis==0?step*50:n*110;float z=GridDepth(x,y,curvature);PointF p=Project(x,y,z,eye);if(last.HasValue)Line(g,Color.FromArgb(48,193,240,244),last.Value.X,last.Value.Y,p.X,p.Y);last=p;}}
 }
 void DrawCardArtwork(Graphics g,Game game){
  RectangleF r=new RectangleF(PointF.Empty,CoverSize(game));
  Image img=GetCover(game);if(img!=null){g.DrawImage(img,r);}else{
   int seed=game.Title.Aggregate(17,(v,c)=>unchecked(v*31+c));Color a=Color.FromArgb(28+Math.Abs(seed%40),45+Math.Abs(seed%50),66+Math.Abs(seed%65));using(var b=new LinearGradientBrush(r,a,Color.FromArgb(12,22,36),55))g.FillRectangle(b,r);
   using(var pen=new Pen(Color.FromArgb(65,143,233,220),1.5f))for(int n=0;n<6;n++)g.DrawEllipse(pen,r.X+r.Width*.2f-n*13,r.Y+25+n*8,r.Width*.65f+n*8,r.Height*.55f);
  }
  if(game.Favorite)TextAt(g,"★",17,Color.FromArgb(149,255,219),r.Right-30,r.Y+11,25);
 }
 Image GetCover(Game game){string path;if(!coverPaths.TryGetValue(game,out path)){path=game.Cover;if(string.IsNullOrEmpty(path)){string cached=Path.Combine(Storage.Folder,"covers",SafeName(game)+".jpg");if(File.Exists(cached))path=cached;}coverPaths[game]=path;}
  if(string.IsNullOrEmpty(path))return null;if(images.ContainsKey(path))return images[path];try{using(var temp=Image.FromFile(path))images[path]=new Bitmap(temp);}catch{images[path]=null;}return images[path];
 }
 public static string SafeName(Game g){return Regex.Replace(g.Platform+"_"+g.Title,@"[^a-zA-Z0-9_-]","_");}
 void ClickScene(object sender,MouseEventArgs e){if(activeGame!=null||returnSince>=0||introSince>=0||revealSince>=0||panel!="")return;PointF p=Logical(e.Location);RebuildHits();Game target=GameAt(p);if(target!=null){selected=target;if(e.Button==MouseButtons.Right)Edit(target);else Launch(target);return;}string id=buttons.Where(x=>x.Value.Contains(p)).Select(x=>x.Key).FirstOrDefault();if(id==null)return;UiSound(true);
  if(id.StartsWith("tab:")){filter=id.Substring(4);page=0;RefreshGames();}else if(id=="full")Fullscreen();else if(id=="scan")Scan();else if(id=="settings")Settings();else if(id=="edit"&&selected!=null)Edit(selected);else if(id=="play"&&selected!=null)Launch(selected);else if(id=="prev")ChangePage(-1);else if(id=="next")ChangePage(1);else if(id=="search")Search();
 }
 void Search(){string value=Prompt("Search library","Game title",query);if(value!=null){query=value;page=0;RefreshGames();}}
 void Scan(){try{Storage.Scan(lib);Storage.Save(lib);status="Scan complete. Existing launch profiles preserved.";RefreshGames();}catch(Exception e){MessageBox.Show(e.Message,"Scan failed");}}
 void Edit(Game g){using(var f=new Profile(g)){if(f.ShowDialog(this)==DialogResult.OK){if(!lib.Games.Contains(g))lib.Games.Add(g);Storage.Save(lib);ClearRenderCache();RefreshGames();}}Invalidate();}
 void Launch(Game game){UiSound(true);try{BeginGame(game);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not launch "+game.Title);}}
 void Return(){RequestExit();}
 protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==Hotkey){Return();return;}base.WndProc(ref m);}
 void Fullscreen(){if(!full){oldBounds=Bounds;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Normal;Bounds=Screen.FromControl(this).Bounds;full=true;}else{FormBorderStyle=FormBorderStyle.Sizable;Bounds=oldBounds;full=false;}lib.Fullscreen=full;Storage.Save(lib);Invalidate();}
 void KeysDown(object sender,KeyEventArgs e){if(panel!=""&&activeGame==null&&returnSince<0){int move=e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-5:e.KeyCode==Keys.Down?5:0;uint pressed=e.KeyCode==Keys.Enter?1u:e.KeyCode==Keys.Escape?2u:0u;HandleNavigation(move,pressed);e.Handled=true;e.SuppressKeyPress=true;return;}if(activeGame!=null||returnSince>=0||introSince>=0||revealSince>=0){if(e.KeyCode==Keys.Escape)Return();e.Handled=true;return;}if(e.KeyCode==Keys.F11){Fullscreen();e.Handled=true;}else if(e.KeyCode==Keys.F2&&selected!=null)Edit(selected);else if(e.KeyCode==Keys.F3)Search();else if(e.KeyCode==Keys.Escape){if(query!=""){query="";RefreshGames();}}else if(e.KeyCode==Keys.PageDown)ChangePage(1);else if(e.KeyCode==Keys.PageUp)ChangePage(-1);else if(e.KeyCode==Keys.Enter&&selected!=null)Launch(selected);else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right||e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){int n=shown.IndexOf(selected)+(e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-5:5);if(shown.Count>0){n=Math.Max(0,Math.Min(shown.Count-1,n));selected=shown[n];page=n/10;FocusGame();UiSound(false);}}}
 public static string Prompt(string title,string label,string value){using(var f=new Form{Text=title,ClientSize=new Size(600,145),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false}){var l=new Label{Text=label,Left=16,Top=15,Width=560};var t=new TextBox{Text=value,Left=16,Top=43,Width=560};var b=new Button{Text="Apply",Left=466,Top=90,Width=110,DialogResult=DialogResult.OK};f.Controls.AddRange(new Control[]{l,t,b});f.AcceptButton=b;return f.ShowDialog()==DialogResult.OK?t.Text:null;}}
 void Settings(){using(var f=new Form{Text="Depth settings",ClientSize=new Size(620,325),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var root=new TextBox{Left=24,Top=53,Width=470,Text=lib.Root};var browse=new Button{Left=506,Top=51,Width=88,Text="Browse"};browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog()){d.SelectedPath=root.Text;if(d.ShowDialog()==DialogResult.OK)root.Text=d.SelectedPath;}};
   var swap=new CheckBox{Left=24,Top=100,Width=420,Text="Swap left and right eyes",Checked=lib.SwapEyes};var depth=new TrackBar{Left=20,Top=159,Width=570,Minimum=0,Maximum=20,Value=(int)(lib.Depth*10),TickFrequency=2};
   
   var tracking=new CheckBox{Left=24,Top=210,Width=480,Text="Head-tracked perspective",Checked=lib.HeadTracking};
   var add=new Button{Left=24,Top=265,Width=140,Text="Add game"};add.Click+=(s,e)=>Edit(new Game{Platform="Custom",Arguments="{rom}"});var save=new Button{Left=450,Top=265,Width=144,Text="Save",DialogResult=DialogResult.OK};
   f.Controls.AddRange(new Control[]{new Label{Left=24,Top=23,Width=500,Text="Library folder"},root,browse,swap,new Label{Left=24,Top=140,Width=500,Text="Depth"},depth,tracking,add,save});if(f.ShowDialog(this)==DialogResult.OK){lib.HeadTracking=tracking.Checked;lib.Root=root.Text;lib.SwapEyes=swap.Checked;lib.Depth=depth.Value/10f;}
  }Storage.Save(lib);Invalidate();}
}

public class Profile:Form {
 Dictionary<string,TextBox> fields=new Dictionary<string,TextBox>();
 public Profile(Game game){Text="Edit — "+game.Title;ClientSize=new Size(780,580);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  string[] names={"Title","Platform","Executable","Arguments","Rom","SteamId","Cover","Note"};string[] labels={"Game title","Platform","Emulator / executable","Arguments","ROM / game file","Steam App ID","Cover image","Notes"};
  for(int i=0;i<names.Length;i++){string key=names[i];Controls.Add(new Label{Text=labels[i],Left=20,Top=16+i*58,Width=650});var box=new TextBox{Left=20,Top=36+i*58,Width=key=="Rom"||key=="Executable"||key=="Cover"?630:740,Text=(string)typeof(Game).GetField(key).GetValue(game)};fields[key]=box;Controls.Add(box);if(key=="Rom"||key=="Executable"||key=="Cover"){var b=new Button{Text="Browse",Left=660,Top=34+i*58,Width=100};b.Click+=(s,e)=>{using(var d=new OpenFileDialog()){d.Filter=key=="Executable"?"Executable|*.exe":key=="Cover"?"Images|*.png;*.jpg;*.jpeg;*.bmp":"Game files|*.*";if(d.ShowDialog()==DialogResult.OK)box.Text=d.FileName;}};Controls.Add(b);}}
  var favorite=new CheckBox{Text="Favorite",Checked=game.Favorite,Left=20,Top=493,Width=150};Controls.Add(favorite);var save=new Button{Text="Save",Left=590,Top=530,Width=170};save.Click+=(s,e)=>{if(string.IsNullOrWhiteSpace(fields["Title"].Text)){MessageBox.Show("Enter a game title.");return;}if(fields["SteamId"].Text!=""&&!Regex.IsMatch(fields["SteamId"].Text,@"^\d+$")){MessageBox.Show("Steam App ID must contain only digits.");return;}foreach(var pair in fields)typeof(Game).GetField(pair.Key).SetValue(game,pair.Value.Text);game.Favorite=favorite.Checked;DialogResult=DialogResult.OK;};Controls.Add(save);var cancel=new Button{Text="Cancel",Left=470,Top=530,Width=110,DialogResult=DialogResult.Cancel};Controls.Add(cancel);CancelButton=cancel;
 }
}

public static class SelfTest {
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  var steam=new Game{SteamId="400"};Assert(Storage.StartInfo(steam).FileName=="steam://rungameid/400","Steam routing");
  bool rejected=false;try{Storage.StartInfo(new Game{SteamId="400 & calc"});}catch{rejected=true;}Assert(rejected,"Invalid Steam IDs rejected");
  string exe=Application.ExecutablePath;var emulator=new Game{Executable=exe,Rom=exe,Arguments="-b -e {rom}"};Assert(Storage.StartInfo(emulator).Arguments=="-b -e \""+exe+"\"","ROM quoting");
  Assert(!Storage.StartInfo(emulator).UseShellExecute,"Emulators do not launch through shell");
  var library=new Library();Storage.Scan(library);int count=library.Games.Count;Storage.Scan(library);Assert(library.Games.Count==count,"Scan is idempotent");
  if(count>0){library.Games[0].Arguments="custom";Storage.Scan(library);Assert(library.Games[0].Arguments=="custom","Custom profiles preserved");}
  var copy=Storage.Json.Deserialize<Library>(Storage.Json.Serialize(library));Assert(copy.Games.Count==count,"Library round trip");
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"PASS: Steam routing, invalid IDs, ROM quoting, shell disabled, scan idempotence, profile preservation, JSON round trip. Discovered "+count+" games.");
 }
}
