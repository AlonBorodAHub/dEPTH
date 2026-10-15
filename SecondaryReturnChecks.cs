using System;using System.Drawing;using System.Reflection;using System.Windows.Forms;
class SecondaryReturnChecks {
 static BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Set(object w,string n,object v){typeof(DepthWindow).GetField(n,F).SetValue(w,v);}
 static object Call(object w,string n,params object[] a){return typeof(DepthWindow).GetMethod(n,F).Invoke(w,a);}
 static void Check(bool yes,string message){if(!yes)throw new Exception(message);}
 [STAThread] static void Main(){
  var state=new LibraryResumeState{Screen=new Rectangle(0,0,3840,2160),SecondaryScreen=new Rectangle(800,2160,2240,1400),Filter="Favorites",SelectedKey="test"};
  var copy=Storage.Json.Deserialize<LibraryResumeState>(Storage.Json.Serialize(state));Check(copy.SecondaryScreen==state.SecondaryScreen&&copy.SelectedKey==state.SelectedKey,"Both display bounds and selection survive process handoff");
  using(var w=new DepthWindow())using(var output=new Bitmap(64,64))using(var g=Graphics.FromImage(output)){
   Set(w,"startupOpening",false);Set(w,"libraryMasked",false);Set(w,"introSince",-1L);Set(w,"revealSince",-1L);Set(w,"companionRevealSince",-5000L);
   var cached=new Bitmap(64,64);using(var c=Graphics.FromImage(cached))c.Clear(Color.White);Set(w,"companionBackground",cached);
   var curtain=(Form)Activator.CreateInstance(typeof(DepthWindow).Assembly.GetType("TransitionCurtain"),new object[]{state.SecondaryScreen});Set(w,"secondaryReturnCurtain",curtain);
   Call(w,"PaintCompanion",g,output.Size);Check(output.GetPixel(32,32).ToArgb()==Color.Black.ToArgb(),"Return curtain keeps companion black even with an old screenshot cached");
   Call(w,"RemoveSecondaryCurtain");Set(w,"activeGame",new Game{Executable="azahar.exe"});Call(w,"PaintCompanion",g,output.Size);Check(output.GetPixel(32,32).ToArgb()==Color.Black.ToArgb(),"Touch-window backing surface remains black during Azahar");
   Set(w,"activeGame",null);Set(w,"companionRevealSince",0L);Set(w,"libraryMasked",true);Call(w,"PaintCompanion",g,output.Size);Check(output.GetPixel(32,32).ToArgb()==Color.Black.ToArgb(),"Hub activation wait cannot reveal the companion");
  }
  Console.WriteLine("PASS: secondary cover state survives restart; old screenshots, active touch gameplay and Hub waiting remain black.");
 }
}
