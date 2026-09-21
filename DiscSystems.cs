using System;
using System.IO;

public static class DiscSystems {
 // Dolphin's WIA/RVZ specification: big-endian disc_type at 0x48.
 // https://github.com/dolphin-emu/dolphin/blob/master/docs/WiaAndRvz.md
 static uint Big(byte[] b,int p){return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
 public static bool IsDolphin(string platform){return platform=="Wii"||platform=="GameCube";}
 public static string Detect(string path,string fallback){
  try{using(var file=File.OpenRead(path)){
   byte[] b=new byte[128];int length=0,n;while(length<b.Length&&(n=file.Read(b,length,b.Length-length))>0)length+=n;
   if(length>=76&&(Big(b,0)==0x52565a01||Big(b,0)==0x57494101)){uint type=Big(b,72);return type==1?"GameCube":type==2?"Wii":fallback;}
   if(length>=32){if(Big(b,28)==0xc2339f3d)return "GameCube";if(Big(b,24)==0x5d1c9ea3)return "Wii";}
  }}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
  return fallback;
 }
 public static bool Separate(Library library){
  bool changed=false;foreach(Game game in library.Games){
   if(!IsDolphin(game.Platform))continue;string platform=Detect(game.Rom,game.Platform);if(platform==game.Platform)continue;
   // Retain artwork cached under the previous platform's name.
   if(string.IsNullOrEmpty(game.Cover)){string cover=Path.Combine(Storage.Folder,"covers",DepthWindow.SafeName(game)+".jpg");if(File.Exists(cover))game.Cover=cover;}
   game.Platform=platform;changed=true;
  }return changed;
 }
}
