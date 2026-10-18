using System;using System.IO;using System.Reflection;using System.Drawing;using System.Windows.Forms;using System.Diagnostics;
class StartupRecoveryChecks {
 static BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static object Call(object w,string method,params object[] args){return typeof(DepthWindow).GetMethod(method,Hidden).Invoke(w,args);}
 static object Field(object w,string name){return typeof(DepthWindow).GetField(name,Hidden).GetValue(w);}
 [STAThread]static void Main(){
  var evidence=typeof(DepthWindow).GetMethod("HubConversionEvidence",BindingFlags.NonPublic|BindingFlags.Static);
  Func<string,long,bool> ready=(text,window)=>(bool)evidence.Invoke(null,new object[]{text,window});
  string original="Conversion Player started with arguments - Window: 123 | isSideBySide: true | cudeCount: 6144\nLensOn!";
  Check(ready(original,123),"Existing Hub format rejected");
  Check(ready("Conversion Player started with arguments - Window : 123 |\r\n isSideBySide : True\nLensOn!",123),"Whitespace/case variants rejected");
  Check(!ready(original,124),"Wrong window accepted");
  Check(!ready(original+"\nLensOff!",123),"Lenses off accepted");
  Check(!ready(original.Replace("true","false"),123),"Non-SBS accepted");
  Check(!ready(original.Replace("LensOn!",""),123),"No lens evidence accepted");
  Check(!ready(original+"\nConversion Player started with arguments - Window: 124 | isSideBySide: true\nLensOn!",123),"Stale session accepted");
  Check(!ready("LensOn!\n"+original.Replace("LensOn!",""),123),"Lens evidence from previous session accepted");
  Check(!ready(null,123),"Missing log accepted");
  string previous=Storage.Folder;string fixture=Path.Combine(Path.GetTempPath(),"depth-startup-"+Guid.NewGuid().ToString("N"));Storage.Folder=fixture;
  Application.EnableVisualStyles();
  try{using(var w=new DepthWindow()){
   Call(w,"Use2DLibrary");Call(w,"PrepareStartup");w.Show();
   var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<1800){Application.DoEvents();System.Threading.Thread.Sleep(15);}
   Check(!w.IsDisposed&&w.Visible,"Clean first run did not remain usable");
   var library=(Library)Field(w,"lib");Check(library.Games.Count==0,"Fixture unexpectedly loaded a personal library");
   Check(!library.Stereo&&!(bool)Field(w,"full"),"2D mode is still stereo/fullscreen");
   Check(!(bool)Field(w,"libraryMasked")&&(long)Field(w,"returnSince")==-1,"2D mode remained behind conversion gate");
   Check((bool)Call(w,"LibraryConversionReady"),"2D mode still requires Hub");
   Call(w,"UpdateHeadTracking",.016);Check(((HeadTracking)Field(w,"headTracker")).Status=="Off","2D acquired head tracker");
   Call(w,"OpenScan");Application.DoEvents();Check((string)Field(w,"panel")=="scan","First-run scan unavailable");
   var gpu=Field(w,"gpu") as IDisposable;if(gpu!=null){gpu.Dispose();typeof(DepthWindow).GetField("gpu",Hidden).SetValue(w,null);}
   using(var frame=new Bitmap(w.ClientSize.Width,w.ClientSize.Height)){w.DrawToBitmap(frame,new Rectangle(Point.Empty,frame.Size));frame.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-2d-check.png"));}
   w.Close();
  }}finally{Storage.Folder=previous;}
  var recovery=typeof(DepthWindow).GetMethod("StartupRecovery",BindingFlags.NonPublic|BindingFlags.Static);
  bool observed=false;using(var timer=new Timer{Interval=100}){timer.Tick+=(sender,e)=>{foreach(Form form in Application.OpenForms){if(form.Text!="dEPTH: 3D connection")continue;observed=form.Visible&&form.TopMost;using(var frame=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(frame,new Rectangle(Point.Empty,frame.Size));frame.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-recovery-check.png"));}form.DialogResult=DialogResult.Cancel;break;}};timer.Start();recovery.Invoke(null,new object[]{"Odyssey 3D Hub has not confirmed conversion. Retry 3D or open the library in windowed 2D. Diagnostic details: data/startup-diagnostics.txt"});timer.Stop();}Check(observed,"Recovery dialog was hidden or not topmost");
  Console.WriteLine("PASS: Hub format variants and stale/wrong/missing evidence; clean empty-library render; windowed 2D recovery; scan access; no head-tracker ownership.");
 }
}
