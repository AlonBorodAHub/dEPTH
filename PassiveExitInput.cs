using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// Read-only HID listener: never changes report mode, enables sensors, sends
// output reports, or disables the emulator's IMU on close.
public sealed class PassiveExitInput : IDisposable {
 public const int Message=0x8064;
 static int sequence;
 public readonly int Token=Interlocked.Increment(ref sequence);
 readonly IntPtr window;readonly Thread worker;readonly object gate=new object();
 volatile bool running=true;SafeFileHandle current;
 public volatile int Reports,FullReports;public volatile string LastReport="",Status="waiting";
 [StructLayout(LayoutKind.Sequential)] struct Attributes {public int size;public ushort vendor,product,version;}
 [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attributes);
 [StructLayout(LayoutKind.Sequential)] struct InterfaceData {public int size;public Guid guid;public uint flags;public IntPtr reserved;}
 [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr parent,uint flags);
 [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr info,ref Guid guid,uint index,ref InterfaceData data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint size,out uint required,IntPtr info);
 [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll")] static extern bool CancelIoEx(SafeFileHandle handle,IntPtr overlapped);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
 static string FindController(){
  Guid guid;HidD_GetHidGuid(out guid);IntPtr set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);if(set==new IntPtr(-1))return null;
  try{for(uint i=0;;i++){var item=new InterfaceData{size=Marshal.SizeOf(typeof(InterfaceData))};if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref item))break;
   uint needed;SetupDiGetDeviceInterfaceDetail(set,ref item,IntPtr.Zero,0,out needed,IntPtr.Zero);if(needed<8)continue;IntPtr detail=Marshal.AllocHGlobal((int)needed);
   try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(SetupDiGetDeviceInterfaceDetail(set,ref item,detail,needed,out needed,IntPtr.Zero)){string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));if(path.IndexOf("057e",StringComparison.OrdinalIgnoreCase)>=0&&path.IndexOf("2009",StringComparison.OrdinalIgnoreCase)>=0)return path;}}finally{Marshal.FreeHGlobal(detail);}
  }}finally{SetupDiDestroyDeviceInfoList(set);}return null;
 }
 public PassiveExitInput(IntPtr target){window=target;worker=new Thread(Listen){IsBackground=true,Name="dEPTH passive exit reports"};worker.Start();}
 public static bool Chord(byte[] report){return report.Length>=6&&(report[0]==0x30||report[0]==0x31)?(report[4]&12)==12:report.Length>=3&&report[0]==0x3f&&(report[2]&12)==12;}
 void Listen(){
  while(running){try{
   string path=FindController();if(path==null){Status="controller unavailable";Thread.Sleep(500);continue;}
   using(var handle=CreateFile(path,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)){
    if(handle.IsInvalid){Status="read-only open failed";Thread.Sleep(500);continue;}
    var attributes=new Attributes{size=Marshal.SizeOf(typeof(Attributes))};if(!HidD_GetAttributes(handle,ref attributes)||attributes.vendor!=0x057e||attributes.product!=0x2009){Status="controller identity mismatch";Thread.Sleep(500);continue;}
    lock(gate){if(!running)return;current=handle;}
    try{using(var stream=new FileStream(handle,FileAccess.Read,512,true)){
     byte[] report=new byte[512];bool armed=false;Status="listening";
     while(running){int length=stream.Read(report,0,report.Length);if(length<3)break;Reports++;if(report[0]==0x30||report[0]==0x31)FullReports++;LastReport=BitConverter.ToString(report,0,Math.Min(12,length));
      if(report[0]!=0x30&&report[0]!=0x31&&report[0]!=0x3f)continue;
      if(!Chord(report))armed=true;else if(armed){armed=false;PostMessage(window,Message,new IntPtr(Token),IntPtr.Zero);}
     }
    }}finally{lock(gate)current=null;}
   }
  }catch(Exception e){Status=e.GetType().Name+": "+e.Message;}if(running)Thread.Sleep(500);}
 }
 public void Dispose(){running=false;lock(gate){if(current!=null&&!current.IsClosed)CancelIoEx(current,IntPtr.Zero);}worker.Join(750);}
}

