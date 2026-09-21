using System;using System.IO;using System.Linq;using System.Reflection;using System.Runtime.Serialization;
class PublicReleaseChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"depth-release-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string games=Path.Combine(root,"games with spaces");Directory.CreateDirectory(Path.Combine(games,"nested"));
   File.WriteAllBytes(Path.Combine(games,"Example.3ds"),new byte[16]);File.WriteAllBytes(Path.Combine(games,"nested","Other.CXI"),new byte[16]);File.WriteAllText(Path.Combine(games,"ignore.txt"),"not a game");File.WriteAllText(Path.Combine(games,"LICENSE"),"not a game");
   var source=new EmulatorSource{Platform="3DS",Executable=typeof(DepthWindow).Assembly.Location,GamesFolder=games,Arguments="{rom}"};
   var library=new Library{Root="",ScanSteam=false,EnableStereoProfiles=false};library.Sources.Add(source);
   Storage.Scan(library);Check(library.Games.Count==2,"Nested scan and extension filtering");
   library.Games[0].Favorite=true;library.Games[0].Arguments="custom {rom}";Storage.Scan(library);Check(library.Games.Count==2&&library.Games[0].Favorite&&library.Games[0].Arguments=="custom {rom}","Rescan preserves profiles and favorites");
   Check(Storage.StartInfo(library.Games[0]).Arguments.Contains("\""+library.Games[0].Rom+"\""),"Paths with spaces are quoted");
   Check(PublicSetup.ScanSource(new EmulatorSource{Platform="PS3",GamesFolder=games}).Count==0,"PS3 ignores extensionless unrelated files");
   Check(PublicSetup.ScanSource(new EmulatorSource{Platform="3DS",GamesFolder=Path.Combine(root,"missing")}).Count==0,"Missing folder is safe");
   string previous=Storage.Folder;Storage.Folder=Path.Combine(root,"data");try{Storage.Save(library);var loaded=Storage.Load();Check(loaded.Sources.Count==1&&loaded.Sources[0].GamesFolder==games&&!loaded.EnableStereoProfiles&&!loaded.ScanSteam,"Portable setup roundtrip");
    var window=(DepthWindow)FormatterServices.GetUninitializedObject(typeof(DepthWindow));typeof(DepthWindow).GetField("lib",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,loaded);typeof(DepthWindow).GetMethod("PrepareStereoProfile",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{new Game{Executable="azahar.exe",Rom="test"}});Check(!File.Exists(StereoProfiles.PathName),"Fresh public setup never writes emulator stereo profiles");
   }finally{Storage.Folder=previous;}
   Console.WriteLine("PASS: independent folders, nested scan, extension filtering, quoted launches, idempotence, profile preservation, setup roundtrip, safe public stereo defaults.");
  }finally{Directory.Delete(root,true);}
 }
}
