using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Threading;

public sealed class ImportMediaResult {public int Covers,Screenshots,Descriptions,Incomplete;}
public static class ImportMedia {
 static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=100};}
 public static string Normalize(string title){title=AutoImport.CleanTitle(title).ToLowerInvariant();title=Regex.Replace(title,@"^the\s+|,\s*the$","");return Regex.Replace(title.Normalize(NormalizationForm.FormD),@"[^a-z0-9]","");}
 static byte[] Fetch(string url,CancellationToken token){
  token.ThrowIfCancellationRequested();ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
  var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="dEPTH/1.2 (game library metadata)";request.Timeout=10000;request.ReadWriteTimeout=10000;
  using(token.Register(()=>request.Abort()))using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
   byte[] buffer=new byte[32768];int n;while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(output.Length+n>32*1024*1024)throw new IOException("Metadata response is too large");output.Write(buffer,0,n);}return output.ToArray();
  }
 }
 static string Repository(string platform){switch(platform){case "3DS":return "Nintendo_-_Nintendo_3DS";case "GameCube":return "Nintendo_-_GameCube";case "Wii":return "Nintendo_-_Wii";case "Wii U":return "Nintendo_-_Wii_U";case "Switch":return "Nintendo_-_Switch";case "Dreamcast":return "Sega_-_Dreamcast";case "PS2":return "Sony_-_PlayStation_2";case "PS3":return "Sony_-_PlayStation_3";default:return "";}}
 static string[] Index(string repo,CancellationToken token){
  string dir=Path.Combine(Storage.Folder,"metadata-cache");Directory.CreateDirectory(dir);string path=Path.Combine(dir,repo+".json");
  try{if(File.Exists(path)&&File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-7))return Json().Deserialize<string[]>(File.ReadAllText(path));}catch{}
  try{var tree=Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Fetch("https://api.github.com/repos/libretro-thumbnails/"+repo+"/git/trees/master?recursive=1",token)));var list=new List<string>();
   foreach(var node in (System.Collections.IEnumerable)tree["tree"]){var item=(Dictionary<string,object>)node;string name=(string)item["path"];if((name.StartsWith("Named_Boxarts/")||name.StartsWith("Named_Snaps/"))&&name.EndsWith(".png"))list.Add(name);}
   File.WriteAllText(path,Json().Serialize(list));return list.ToArray();
  }catch{token.ThrowIfCancellationRequested();try{return Json().Deserialize<string[]>(File.ReadAllText(path));}catch{return new string[0];}}
 }
 static string ArtworkUrl(string repo,string[] index,string kind,string title){
  string normal=Normalize(title);string match=index.Where(p=>p.StartsWith(kind+"/")&&Normalize(Path.GetFileNameWithoutExtension(p))==normal).OrderBy(p=>p.Contains("(USA")?0:p.Contains("(World")?1:p.Contains("(Europe")?2:3).ThenBy(p=>p).FirstOrDefault();
  return match==null?"":"https://raw.githubusercontent.com/libretro-thumbnails/"+repo+"/master/"+string.Join("/",match.Split('/').Select(Uri.EscapeDataString));
 }
 static bool ImageFile(string url,string target,CancellationToken token){
  if(File.Exists(target))return true;if(url=="")return false;
  try{byte[] data=null;for(int i=0;i<4;i++){data=Fetch(url,token);if(data.Length>180)break;string name=Encoding.UTF8.GetString(data).Trim();if(!Regex.IsMatch(name,@"^[^/\\]+\.png$"))return false;url=url.Substring(0,url.LastIndexOf('/')+1)+Uri.EscapeDataString(name);}
   using(var memory=new MemoryStream(data))using(var image=Image.FromStream(memory)){if(image.Width<2||image.Height<2)return false;}
   Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllBytes(target+".tmp",data);File.Move(target+".tmp",target);return true;
  }catch{token.ThrowIfCancellationRequested();return false;}
 }
 static void Description(Game game,CompanionMedia media,CancellationToken token,ref bool available){
  if(media.Summary!=""||!available)return;
  foreach(string title in new[]{game.Title,game.Title+" (video game)"})try{
   var wiki=Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Fetch("https://en.wikipedia.org/api/rest_v1/page/summary/"+Uri.EscapeDataString(title.Replace(' ','_')),token)));
   if(!wiki.ContainsKey("type")||(string)wiki["type"]!="standard"||!wiki.ContainsKey("extract"))continue;
   string extract=(string)wiki["extract"],description=wiki.ContainsKey("description")?(string)wiki["description"]:"";
   if(!Regex.IsMatch(description+" "+extract.Substring(0,Math.Min(extract.Length,250)),@"(?i)\b(video game|racing game|platform game|role-playing game|action game|game compilation)\b"))continue;
   string canonical=wiki.ContainsKey("title")?(string)wiki["title"]:title;if(Normalize(canonical)!=Normalize(game.Title))continue;
   media.Summary=Regex.Split(extract,@"(?<=[.!?])\s+")[0];if(media.Summary.Length>440)media.Summary=media.Summary.Substring(0,437)+"…";
   media.Details=game.Platform+" · "+description;media.InfoSource="https://en.wikipedia.org/wiki/"+Uri.EscapeDataString(canonical.Replace(' ','_'));media.Credit=(media.Credit==""?"":media.Credit+" · ")+"Wikipedia (CC BY-SA)";return;
  }catch(WebException ex){token.ThrowIfCancellationRequested();var response=ex.Response as HttpWebResponse;if(response==null||response.StatusCode!=HttpStatusCode.NotFound){available=false;return;}}catch{token.ThrowIfCancellationRequested();}
 }
 static void Save(Dictionary<string,CompanionMedia> media){string path=Path.Combine(Storage.Folder,"companion-media.json");File.WriteAllText(path+".tmp",Json().Serialize(media));if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);}
 public static ImportMediaResult Enrich(IEnumerable<Game> games,Action<string> progress,CancellationToken token){
  var result=new ImportMediaResult();Dictionary<string,CompanionMedia> all;string mediaPath=Path.Combine(Storage.Folder,"companion-media.json");
  if(File.Exists(mediaPath))all=Json().Deserialize<Dictionary<string,CompanionMedia>>(File.ReadAllText(mediaPath));else all=new Dictionary<string,CompanionMedia>();
  bool descriptionsAvailable=true;var indexes=new Dictionary<string,string[]>();var list=games.ToList();int done=0;
  foreach(var game in list){token.ThrowIfCancellationRequested();progress("Artwork and info "+(++done)+" / "+list.Count+": "+game.Title);string key=DepthWindow.SafeName(game);CompanionMedia media;if(!all.TryGetValue(key,out media))media=new CompanionMedia{Details=game.Platform};
   string repo=Repository(game.Platform);string[] index=new string[0];string cover=game.Cover;if(cover=="")cover=Path.Combine(Storage.Folder,"covers",key+".jpg");
   bool needsCover=!File.Exists(cover),needsShot=media.Image==""||!File.Exists(Path.Combine(Storage.Folder,media.Image));
   if(repo!=""&&(needsCover||needsShot)){if(!indexes.TryGetValue(repo,out index)){index=Index(repo,token);indexes[repo]=index;}}
   string coverUrl=ArtworkUrl(repo,index,"Named_Boxarts",game.Title);if(needsCover&&ImageFile(coverUrl,cover,token)){game.Cover=cover;media.CoverSource=coverUrl;if(!media.Credit.Contains("Libretro"))media.Credit+=(media.Credit==""?"":" · ")+"Libretro thumbnails";result.Covers++;}else if(File.Exists(cover)&&game.Cover=="")game.Cover=cover;
   if(needsShot){string url=ArtworkUrl(repo,index,"Named_Snaps",game.Title);string relative="screenshots/"+key+".png";if(ImageFile(url,Path.Combine(Storage.Folder,relative),token)){media.Image=relative;media.ImageSource=url;if(!media.Credit.Contains("Libretro"))media.Credit+=(media.Credit==""?"":" · ")+"Libretro thumbnails";result.Screenshots++;}}
   bool had=media.Summary!="";Description(game,media,token,ref descriptionsAvailable);if(!had&&media.Summary!="")result.Descriptions++;
   if(!File.Exists(cover)||media.Image==""||media.Summary=="")result.Incomplete++;
   all[key]=media;Save(all);if(game.Note==""&&media.Summary!="")game.Note=media.Summary;
  }
  return result;
 }
}

