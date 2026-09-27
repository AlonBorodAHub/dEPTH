using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// An independent lifetime guard restores the user's cursor scheme even when
// the UI crashes. It also spans the overlapping processes used for 3D return.
static class SystemCursorGuard {
 const string MutexName=@"Local\dEPTH.SystemCursorGuard";
 static readonly uint[] CursorIds={32512,32513,32514,32515,32516,32640,32641,32642,32643,32644,32645,32646,32648,32649,32650,32651};
 [DllImport("user32.dll")] static extern IntPtr CreateCursor(IntPtr instance,int x,int y,int width,int height,byte[] andMask,byte[] xorMask);
 [DllImport("user32.dll")] static extern bool SetSystemCursor(IntPtr cursor,uint id);
 [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
 [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr instance,IntPtr name);
 [DllImport("user32.dll")] static extern IntPtr CopyIcon(IntPtr icon);
 [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action,uint parameter,IntPtr value,uint flags);
 public static void Start(){
  string name=@"Local\DepthCursorReady-"+Guid.NewGuid().ToString("N");
  using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,name)){
   using(var child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--cursor-guard "+name){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){
    if(!ready.WaitOne(3000))throw new InvalidOperationException("The cursor guard could not start.");
   }
  }
 }
 static void Signal(string name){using(var ready=EventWaitHandle.OpenExisting(name))ready.Set();}
 static bool AppRunning(){
  int self=Process.GetCurrentProcess().Id;string path=Application.ExecutablePath;
  foreach(var p in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(path)))using(p){try{if(p.Id!=self&&!p.HasExited&&string.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase))return true;}catch{}}
  return false;
 }
 static void Hide(){
  byte[] mask=Enumerable.Repeat((byte)255,128).ToArray();
  foreach(uint id in CursorIds){IntPtr cursor=CreateCursor(IntPtr.Zero,0,0,32,32,mask,new byte[128]);if(cursor!=IntPtr.Zero&&!SetSystemCursor(cursor,id))DestroyCursor(cursor);}
 }
 public static void Run(string ready){
  using(var mutex=new Mutex(false,MutexName)){
   bool owned=false;var originals=new System.Collections.Generic.Dictionary<uint,IntPtr>();try{try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
    if(!owned){Signal(ready);return;}
    foreach(uint id in CursorIds){IntPtr cursor=LoadCursor(IntPtr.Zero,new IntPtr(id));if(cursor!=IntPtr.Zero){IntPtr copy=CopyIcon(cursor);if(copy!=IntPtr.Zero)originals[id]=copy;}}
    Hide();Signal(ready);int empty=0,ticks=0;
    while(empty<4){Thread.Sleep(250);empty=AppRunning()?0:empty+1;if(empty==0&&++ticks%4==0)Hide();}
   }finally{if(owned){SystemParametersInfo(0x0057,0,IntPtr.Zero,0);foreach(var original in originals)if(!SetSystemCursor(original.Value,original.Key))DestroyCursor(original.Value);mutex.ReleaseMutex();}}
  }
 }
}
