using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
public class StereoProfile {
 public double OriginalDepth,OriginalConvergence,Depth,Convergence;
 public string Backend="";
 public void Reset(){Depth=OriginalDepth;Convergence=OriginalConvergence;}
}
public static class StereoProfiles {
 static readonly CultureInfo C=CultureInfo.InvariantCulture;
 public static string PathName {get{return Path.Combine(Storage.Folder,"stereo-profiles.json");}}
 public static Dictionary<string,StereoProfile> Read(){return File.Exists(PathName)?Storage.Json.Deserialize<Dictionary<string,StereoProfile>>(File.ReadAllText(PathName)):new Dictionary<string,StereoProfile>();}
 public static void Save(Dictionary<string,StereoProfile> profiles){string temp=PathName+".tmp";File.WriteAllText(temp,Storage.Json.Serialize(profiles));if(File.Exists(PathName))File.Replace(temp,PathName,PathName+".bak");else File.Move(temp,PathName);}
 public static double Number(string text,string key,double fallback){var m=Regex.Match(text,@"(?m)^"+Regex.Escape(key)+@"\s*=\s*([-+0-9.eE]+)");double value;return m.Success&&double.TryParse(m.Groups[1].Value,NumberStyles.Float,C,out value)&&!double.IsNaN(value)&&!double.IsInfinity(value)?value:fallback;}
 public static string SetNumber(string text,string key,double value){string pattern=@"(?m)^"+Regex.Escape(key)+@"[ \t]*=.*$";if(!Regex.IsMatch(text,pattern))throw new InvalidDataException("Missing stereo setting: "+key);return Regex.Replace(text,pattern,key+" = "+value.ToString("R",C));}
 public static double AzaharStep(double value,int direction){return direction>0?Math.Min(255,(Math.Floor(value/5)+1)*5):Math.Max(0,(Math.Ceiling(value/5)-1)*5);}
}
public partial class DepthWindow {
 Dictionary<string,StereoProfile> stereoProfiles;StereoProfile stereoProfile;
 bool resettingStereo;long nextStereoReset;
 string AzaharConfig {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Azahar\config\qt-config.ini");}}
 void PrepareStereoProfile(Game game){
  stereoProfile=null;resettingStereo=false;
  if(!lib.EnableStereoProfiles)return;
  string backend=Path.GetFileNameWithoutExtension(game.Executable).ToLowerInvariant();
  if(backend!="flycast"&&backend!="azahar")return;
  stereoProfiles=StereoProfiles.Read();
  if(!stereoProfiles.TryGetValue(game.Key,out stereoProfile)){
   StereoProfile defaults;
   if(!stereoProfiles.TryGetValue("default:"+backend,out defaults)){
    string config=backend=="azahar"?AzaharConfig:Path.Combine(Path.GetDirectoryName(game.Executable),"d3dxdm.ini");
    if(!File.Exists(config))return;
    string current=File.ReadAllText(config);double depth=StereoProfiles.Number(current,backend=="azahar"?"factor_3d":"dm_separation",double.NaN),convergence=backend=="azahar"?0:StereoProfiles.Number(current,"dm_convergence",double.NaN);
    if(double.IsNaN(depth)||double.IsNaN(convergence))return;
    defaults=new StereoProfile{Backend=backend,OriginalDepth=depth,Depth=depth,OriginalConvergence=convergence,Convergence=convergence};
   }
   stereoProfile=new StereoProfile{Backend=backend,OriginalDepth=defaults.OriginalDepth,Depth=defaults.OriginalDepth,OriginalConvergence=defaults.OriginalConvergence,Convergence=defaults.OriginalConvergence};stereoProfiles[game.Key]=stereoProfile;StereoProfiles.Save(stereoProfiles);
  }
  if(backend=="azahar"){
   string text=File.ReadAllText(AzaharConfig);File.WriteAllText(AzaharConfig,StereoProfiles.SetNumber(text,"factor_3d",stereoProfile.Depth));
  }else{
   string path=Path.Combine(Path.GetDirectoryName(game.Executable),"d3dxdm.ini"),text=File.ReadAllText(path);
   text=StereoProfiles.SetNumber(text,"dm_separation",stereoProfile.Depth);text=StereoProfiles.SetNumber(text,"dm_convergence",stereoProfile.Convergence);
   text=Regex.Replace(text,@"(?s)\r?\n; DEPTH PER-GAME RESET BEGIN.*?; DEPTH PER-GAME RESET END\r?\n?","");
   text+="\n; DEPTH PER-GAME RESET BEGIN\n[KeyDepthOriginal]\nKey = no_modifiers F21\nseparation = "+stereoProfile.OriginalDepth.ToString(CultureInfo.InvariantCulture)+"\nconvergence = "+stereoProfile.OriginalConvergence.ToString(CultureInfo.InvariantCulture)+"\n; DEPTH PER-GAME RESET END\n";
   File.WriteAllText(path,text);
  }
 }
 void CaptureStereoProfile(){
  if(stereoProfile==null||activeGame==null)return;
  if(stereoProfile.Backend=="azahar")stereoProfile.Depth=StereoProfiles.Number(File.ReadAllText(AzaharConfig),"factor_3d",stereoProfile.Depth);
  else {
   string path=Path.Combine(Path.GetDirectoryName(activeGame.Executable),"d3dx_user.ini");
   if(File.Exists(path)){string text=File.ReadAllText(path);stereoProfile.Depth=StereoProfiles.Number(text,"$depth_saved_separation",stereoProfile.Depth);stereoProfile.Convergence=StereoProfiles.Number(text,"$depth_saved_convergence",stereoProfile.Convergence);}
  }
  StereoProfiles.Save(stereoProfiles);stereoProfile=null;resettingStereo=false;
 }
 void SaveAzaharAdjustment(int move){if(stereoProfile==null||stereoProfile.Backend!="azahar"||Math.Abs(move)!=2)return;stereoProfile.Depth=StereoProfiles.AzaharStep(stereoProfile.Depth,move);StereoProfiles.Save(stereoProfiles);}
 void BeginStereoReset(){
  if(stereoProfile==null)return;
  ReleaseStereoKey();
  if(stereoProfile.Backend=="flycast"){
   if(SendStereoKey(0x84,false)){stereoHeldKey=0x84;stereoReleaseAt=frameClock.ElapsedMilliseconds+70;stereoProfile.Reset();StereoProfiles.Save(stereoProfiles);}
  }else {resettingStereo=true;nextStereoReset=0;}
 }
}
