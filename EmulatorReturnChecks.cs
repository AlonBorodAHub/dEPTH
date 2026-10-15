using System;using System.Diagnostics;using System.Drawing;using System.Reflection;using System.Threading;using System.Windows.Forms;
class EmulatorReturnChecks {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object w,string name,object value){typeof(DepthWindow).GetField(name,F).SetValue(w,value);}
 static object Get(object w,string name){return typeof(DepthWindow).GetField(name,F).GetValue(w);}
 static object Call(object w,string name,params object[] values){return typeof(DepthWindow).GetMethod(name,F).Invoke(w,values);}
 [STAThread] static void Main(){using(var w=new DepthWindow())using(var emulator=new Form()){
  ((System.Windows.Forms.Timer)Get(w,"navigationTimer")).Stop();((System.Windows.Forms.Timer)Get(w,"animation")).Stop();
  emulator.Show();Set(w,"activeGame",new Game{Executable="test-emulator.exe"});Set(w,"gameWindow",emulator.Handle);Set(w,"playing",true);Set(w,"libraryMasked",false);Call(w,"WatchGameVisibility");
  emulator.Close();var time=Stopwatch.StartNew();while(Get(w,"curtain")==null&&time.ElapsedMilliseconds<1500){Application.DoEvents();Thread.Sleep(10);}
  if(Get(w,"curtain")==null||!(bool)Get(w,"libraryMasked"))throw new Exception("Native emulator close must immediately cover and mask the library");
  Set(w,"startupOpening",false);using(var b=new Bitmap(80,80))using(var g=Graphics.FromImage(b)){g.Clear(Color.Magenta);Call(w,"RenderOpening",g);for(int y=0;y<80;y++)for(int x=0;x<80;x++)if(b.GetPixel(x,y).ToArgb()!=Color.Black.ToArgb())throw new Exception("Underlying library must present pure black");}
 }Console.WriteLine("PASS: native window close triggers black curtain; underlying library renders black.");}
}
