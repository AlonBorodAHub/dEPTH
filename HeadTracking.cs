using System;using System.Diagnostics;using System.Runtime.InteropServices;using System.Threading;
public sealed class HeadTracking:IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct Pair{public ulong id,time;public double lx,ly,lz,rx,ry,rz;}
 delegate void Callback(Pair p);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("SimulatedRealityCore.dll")]static extern IntPtr newSRContext();
 [DllImport("SimulatedRealityCore.dll")]static extern void initializeSRContext(IntPtr context);
 [DllImport("SimulatedRealityCore.dll")]static extern void deleteSRContext(IntPtr context);
 [DllImport("SimulatedRealityFaceTrackers.dll")]static extern IntPtr createEyeTracker(IntPtr context);
 [DllImport("SimulatedRealityFaceTrackers.dll")]static extern IntPtr createEyePairListener(IntPtr tracker,Callback callback);
 [DllImport("SimulatedRealityFaceTrackers.dll")]static extern void deleteEyePairListener(IntPtr listener);
 readonly object gate=new object();readonly AutoResetEvent changed=new AutoResetEvent(false);readonly Callback callback;Thread worker;
 volatile bool enabled,disposed;double x,y;long received;public string Status="Off";
 public HeadTracking(){callback=Accept;}
 public void SetEnabled(bool value){if(disposed||enabled==value)return;enabled=value;if(worker==null){worker=new Thread(Run){IsBackground=true,Name="dEPTH head tracking"};worker.Start();}changed.Set();}
 void Accept(Pair p){double nx=(p.lx+p.rx)/2,ny=(p.ly+p.ry)/2,nz=(p.lz+p.rz)/2;if(!Valid(nx,ny,nz))return;lock(gate){x=nx;y=ny;received=Stopwatch.GetTimestamp();}}
 public static bool Valid(double x,double y,double z){return !double.IsNaN(x)&&!double.IsNaN(y)&&!double.IsNaN(z)&&Math.Abs(x)<1500&&Math.Abs(y)<1500&&z>100&&z<2000;}
 public bool Read(out double px,out double py){lock(gate){px=x;py=y;return enabled&&received!=0&&(Stopwatch.GetTimestamp()-received)/(double)Stopwatch.Frequency<.25;}}
 void Run(){IntPtr context=IntPtr.Zero,listener=IntPtr.Zero;bool loaded=false;try{while(!disposed){
  if(enabled&&context==IntPtr.Zero){try{
   if(!loaded){string root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"LeiaSR\Platform\bin");foreach(string dll in new[]{"SimulatedRealityCore.dll","SimulatedRealityFaceTrackers.dll"})if(LoadLibraryEx(System.IO.Path.Combine(root,dll),IntPtr.Zero,8)==IntPtr.Zero)throw new InvalidOperationException("Runtime unavailable");loaded=true;}
   context=newSRContext();if(context==IntPtr.Zero)throw new InvalidOperationException("Runtime unavailable");var tracker=createEyeTracker(context);if(tracker==IntPtr.Zero)throw new InvalidOperationException("Tracker unavailable");listener=createEyePairListener(tracker,callback);if(listener==IntPtr.Zero)throw new InvalidOperationException("Tracker unavailable");initializeSRContext(context);Status="Ready";
  }catch(Exception){Status="Unavailable";if(listener!=IntPtr.Zero){deleteEyePairListener(listener);listener=IntPtr.Zero;}if(context!=IntPtr.Zero){deleteSRContext(context);context=IntPtr.Zero;}changed.WaitOne(3000);}}
  if(!enabled&&context!=IntPtr.Zero){if(listener!=IntPtr.Zero){deleteEyePairListener(listener);listener=IntPtr.Zero;}deleteSRContext(context);context=IntPtr.Zero;lock(gate)received=0;Status="Off";}
  changed.WaitOne(100);
 }}finally{if(listener!=IntPtr.Zero)deleteEyePairListener(listener);if(context!=IntPtr.Zero)deleteSRContext(context);changed.Dispose();}}
 public void Dispose(){if(disposed)return;disposed=true;if(worker!=null){try{changed.Set();}catch(ObjectDisposedException){}}else changed.Dispose();}
}
public partial class DepthWindow {
 readonly HeadTracking headTracker=new HeadTracking();float headX,headY;double headOriginX,headOriginY;bool headCentered;
 void UpdateHeadTracking(double dt){bool enabled=lib.HeadTracking&&activeGame==null&&!libraryMasked;headTracker.SetEnabled(enabled);double x,y;bool fresh=enabled&&headTracker.Read(out x,out y);float tx=0,ty=0;
  // Calibrate at acquisition; a lost face never leaves the scene stuck off-center.
  if(fresh){headTracker.Read(out x,out y);if(!headCentered){headOriginX=x;headOriginY=y;headCentered=true;}if(gpu!=null&&!libraryMasked&&introSince<0){tx=(float)Math.Max(-90,Math.Min(90,(x-headOriginX)*1.2));ty=(float)Math.Max(-65,Math.Min(65,-(y-headOriginY)*1.2));}}
  else headCentered=false;
  float blend=(float)(1-Math.Exp(-Math.Max(0,dt)*28));headX+=(tx-headX)*blend;headY+=(ty-headY)*blend;if(!enabled){headX=0;headY=0;}
  if(gpu!=null)gpu.HeadY=headY;
 }
}
