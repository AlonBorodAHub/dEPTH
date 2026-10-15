using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;

public sealed class AutoImportResult {
 public int Added,Existing,Unknown,MissingEmulator;public List<Game> Games=new List<Game>();
}
public static class AutoImport {
 public static string CleanTitle(string name){
  name=Regex.Replace(name,@"\s*\([^)]*\)|\s*\[[^\]]*\]","");
  name=Regex.Replace(name,@"[_]+"," ").Trim();
  name=Regex.Replace(name,@"^(.*?), The(.*)$","The $1$2",RegexOptions.IgnoreCase);
  return Regex.Replace(name,@"\s+"," ").Trim();
 }
 public static string Detect(string file){
  string ext=Path.GetExtension(file).ToLowerInvariant();
  string dolphin=DiscSystems.Detect(file,"");if(dolphin!="")return dolphin;
  if(ext==".3ds"||ext==".cci"||ext==".cxi")return "3DS";
  if(ext==".xci"||ext==".nsp"||ext==".nsz"||ext==".xcz")return "Switch";
  if(ext==".wbfs")return "Wii";if(ext==".gcm")return "GameCube";
  if(ext==".gdi"||ext==".cdi")return "Dreamcast";
  if(ext==".wua"||ext==".wud"||ext==".wux"||ext==".rpx")return "Wii U";
  if(Path.GetFileName(file).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(Path.GetDirectoryName(file)).Equals("USRDIR",StringComparison.OrdinalIgnoreCase))return "PS3";
  try{using(var f=File.OpenRead(file)){byte[] b=new byte[(int)Math.Min(f.Length,2*1024*1024)];int n=f.Read(b,0,b.Length);string text=Encoding.ASCII.GetString(b,0,n);
   if(text.Contains("BOOT2")&&Regex.IsMatch(text,@"(?i)(SLUS|SLES|SCUS|SCES|SLPS|SLPM|SCPS)[_\-]"))return "PS2";
   if(text.Contains("SEGA SEGAKATANA"))return "Dreamcast";
   if(text.Contains("PS3_VOLUME"))return "PS3";
   if(n>=260&&(Encoding.ASCII.GetString(b,256,4)=="NCSD"||Encoding.ASCII.GetString(b,256,4)=="NCCH"))return "3DS";
  }}catch(IOException){}catch(UnauthorizedAccessException){}
  string dirs=Path.GetDirectoryName(file).Replace('_',' ').Replace('-',' ');
  foreach(var pair in new[]{new[]{"PS2","ps2|playstation ?2|pcsx2"},new[]{"PS3","ps3|playstation ?3|rpcs3"},new[]{"Dreamcast","dreamcast|flycast"},new[]{"GameCube","gamecube|game cube|ngc"},new[]{"Wii U","wii ?u|cemu"},new[]{"Wii","wii"},new[]{"3DS","3ds|azahar|citra"},new[]{"Switch","switch|yuzu|ryujinx"}})
   if(Regex.IsMatch(dirs,@"(?i)(^|[\\/ ])("+pair[1]+@")([\\/ ]|$)"))return pair[0];
  return "Unknown";
 }
 static bool Candidate(string f){string ext=Path.GetExtension(f).ToLowerInvariant();return new[]{".3ds",".cci",".cxi",".iso",".chd",".cso",".gdi",".cdi",".gcm",".rvz",".wia",".wbfs",".wua",".wud",".wux",".rpx",".xci",".nsp",".nsz",".xcz"}.Contains(ext)||Path.GetFileName(f).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase);}
 public static string Emulator(Library library,string platform,IEnumerable<string> executables){
  var old=library.Games.FirstOrDefault(g=>(g.Platform==platform||DiscSystems.IsDolphin(platform)&&DiscSystems.IsDolphin(g.Platform))&&File.Exists(g.Executable));if(old!=null)return old.Executable;
  var source=(library.Sources??new List<EmulatorSource>()).FirstOrDefault(s=>s.Platform==platform&&File.Exists(s.Executable));if(source!=null)return source.Executable;
  string pattern=platform=="3DS"?"^(azahar|citra-qt)":DiscSystems.IsDolphin(platform)?"^dolphin$":platform=="PS2"?"^pcsx2":platform=="PS3"?"^rpcs3$":platform=="Dreamcast"?"^flycast$":platform=="Wii U"?"^cemu$":platform=="Switch"?"^(yuzu|ryujinx|suyu|sudachi|eden)$":"(?!)";
  return executables.FirstOrDefault(p=>Regex.IsMatch(Path.GetFileNameWithoutExtension(p),pattern,RegexOptions.IgnoreCase))??"";
 }
 public static AutoImportResult Scan(Library library,string folder,Action<string> progress,CancellationToken cancel){
  if(!Directory.Exists(folder))throw new DirectoryNotFoundException("Choose an existing ROM folder.");
  var result=new AutoImportResult();var files=new List<string>();int visited=0;
  foreach(string file in PublicSetup.Files(folder)){cancel.ThrowIfCancellationRequested();if(Candidate(file))files.Add(file);if(++visited%250==0)progress("Looking for games… "+visited+" files checked");}
  progress("Detecting systems and installed emulators…");
  var executables=new List<string>();var roots=new[]{library.Root,@"C:\Games\Emulators",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Azahar"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Dolphin"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")};
  foreach(string root in roots.Where(r=>!string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))foreach(string file in PublicSetup.Files(root)){cancel.ThrowIfCancellationRequested();if(Path.GetExtension(file).Equals(".exe",StringComparison.OrdinalIgnoreCase))executables.Add(file);}
  var keys=new HashSet<string>(library.Games.Select(g=>g.Key),StringComparer.OrdinalIgnoreCase);
  foreach(string file in files){cancel.ThrowIfCancellationRequested();if(keys.Contains(file)){result.Existing++;var old=library.Games.First(g=>string.Equals(g.Key,file,StringComparison.OrdinalIgnoreCase));if(old.Platform=="Unknown"){string detected=Detect(file);if(detected!="Unknown"){old.Platform=detected;old.Arguments=PublicSetup.Arguments(detected);}}if(!File.Exists(old.Executable))old.Executable=Emulator(library,old.Platform,executables);continue;}
   // An installed title has one launch target; ignore extra RPX binaries and disc track files.
   if(Path.GetExtension(file).Equals(".rpx",StringComparison.OrdinalIgnoreCase)&&files.Any(f=>f!=file&&Path.GetExtension(f).Equals(".rpx",StringComparison.OrdinalIgnoreCase)&&Path.GetDirectoryName(f)==Path.GetDirectoryName(file)&&string.CompareOrdinal(f,file)<0))continue;
   string platform=Detect(file);string title=CleanTitle(Path.GetFileNameWithoutExtension(file));
   if(platform=="PS3"&&Path.GetFileName(file).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase)){string dir=Path.GetDirectoryName(Path.GetDirectoryName(file));string sfo=Path.Combine(dir,"PARAM.SFO");string category=Storage.SfoField(sfo,"CATEGORY");if(category!=""&&category!="HG"&&category!="DG")continue;title=Storage.SfoField(sfo,"TITLE");if(title=="")title=CleanTitle(Path.GetFileName(dir));}
   if(platform=="Wii U"&&Path.GetExtension(file).Equals(".rpx",StringComparison.OrdinalIgnoreCase)){string meta=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(file)),"meta","meta.xml");try{var xml=new System.Xml.XmlDocument();xml.Load(meta);var node=xml.SelectSingleNode("//longname_en");if(node!=null)title=node.InnerText.Replace('\n',' ').Trim();}catch{} }
   string exe=Emulator(library,platform,executables);var game=new Game{Title=title,Platform=platform,Rom=file,Executable=exe,Arguments=PublicSetup.Arguments(platform)};
   if(platform=="Unknown")result.Unknown++;else if(exe=="")result.MissingEmulator++;
   if(library.HiddenGameKeys!=null)library.HiddenGameKeys.Remove(game.Key);if(library.HiddenPlatforms!=null)library.HiddenPlatforms.Remove(platform);
   library.Games.Add(game);keys.Add(game.Key);result.Games.Add(game);result.Added++;progress("Found "+result.Added+": "+title+" ("+platform+")");
  }
  return result;
 }
}
