using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public sealed class ControllerInput : IDisposable {
 readonly Dictionary<int,IntPtr> pads=new Dictionary<int,IntPtr>();
 readonly HashSet<int> armed=new HashSet<int>();
 bool initialized,unavailable;
 bool motionEnabled;
 long motionUnhealthySince=-1;
 readonly Dictionary<int,GyroPointerFilter> motion=new Dictionary<int,GyroPointerFilter>();
 readonly Dictionary<int,GyroStreamWatchdog> streams=new Dictionary<int,GyroStreamWatchdog>();
 float[] rememberedMotionBias;
 public string MotionStatus="disabled";
 public bool MotionReady;public float GyroX,GyroY;
 public float[] RememberedMotionBias {get{return rememberedMotionBias==null?null:(float[])rememberedMotionBias.Clone();}}
 public void RestoreMotionBias(float[] calibration){rememberedMotionBias=calibration==null?null:(float[])calibration.Clone();}
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GameControllerHasSensor(IntPtr pad,int sensor);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GameControllerSetSensorEnabled(IntPtr pad,int sensor,int enabled);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GameControllerGetSensorDataWithTimestamp(IntPtr pad,int sensor,out ulong timestamp,float[] data,int count);
 public void SetMotionEnabled(bool enabled){if(motionEnabled==enabled)return;Dispose();motionUnhealthySince=-1;motionEnabled=enabled;}
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_SetHint(string name,string value);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_InitSubSystem(uint flags);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern void SDL_QuitSubSystem(uint flags);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern void SDL_PumpEvents();
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_NumJoysticks();
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_IsGameController(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_JoystickGetDeviceInstanceID(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_GameControllerOpen(int index);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern void SDL_GameControllerClose(IntPtr pad);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern int SDL_GameControllerGetAttached(IntPtr pad);
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern byte SDL_GameControllerGetButton(IntPtr pad,int button);
 const int LeftStick=7,RightStick=8;
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern short SDL_GameControllerGetAxis(IntPtr pad,int axis);
 readonly Dictionary<int,uint> previous=new Dictionary<int,uint>();
 readonly Dictionary<int,int> direction=new Dictionary<int,int>();
 readonly Dictionary<int,long> repeatAt=new Dictionary<int,long>();
 readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 public uint Pressed;public int Move;public int StereoMove;
 readonly Dictionary<int,int> stereoDirection=new Dictionary<int,int>();
 readonly Dictionary<int,long> stereoRepeatAt=new Dictionary<int,long>();
 public static int StereoDirection(short left,short right,uint buttons){if(left<16000||right<16000)return 0;if((buttons&0x600)==0x600)return 3;uint d=buttons&0x7800;return d==(1u<<13)?-1:d==(1u<<14)?1:d==(1u<<11)?2:d==(1u<<12)?-2:0;}
 [DllImport("SDL2.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SDL_GameControllerName(IntPtr pad);
 public string Status="";
 public void Reset(){armed.Clear();stereoDirection.Clear();stereoRepeatAt.Clear();}
 public bool Poll(){
  Pressed=0;Move=0;StereoMove=0;GyroX=GyroY=0;MotionReady=false;MotionStatus=motionEnabled?"waiting for controller":"disabled";
  if(unavailable)return false;
  try{
   // Own motion only in the library. Release it BEFORE launching an emulator,
   // then use passive buttons while the emulator owns the sensor/report mode.
   if(!initialized){SDL_SetHint("SDL_JOYSTICK_HIDAPI_SWITCH",motionEnabled?"1":"0");SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");if(SDL_InitSubSystem(0x2000)!=0){Status="Controller initialization failed";return false;}initialized=true;}
   SDL_PumpEvents();
   foreach(int id in new List<int>(pads.Keys))if(SDL_GameControllerGetAttached(pads[id])==0){SDL_GameControllerClose(pads[id]);pads.Remove(id);motion.Remove(id);streams.Remove(id);armed.Remove(id);previous.Remove(id);direction.Remove(id);repeatAt.Remove(id);stereoDirection.Remove(id);stereoRepeatAt.Remove(id);}
   for(int i=0;i<SDL_NumJoysticks();i++){int id=SDL_JoystickGetDeviceInstanceID(i);if(id<0||pads.ContainsKey(id)||SDL_IsGameController(i)==0)continue;IntPtr pad=SDL_GameControllerOpen(i);if(pad!=IntPtr.Zero)pads.Add(id,pad);}
   bool exit=false;Status="";
   foreach(var entry in pads){bool left=SDL_GameControllerGetButton(entry.Value,LeftStick)!=0,right=SDL_GameControllerGetButton(entry.Value,RightStick)!=0;Status+=Marshal.PtrToStringAnsi(SDL_GameControllerName(entry.Value))+": L3="+left+", R3="+right+"; ";bool both=left&&right;
    if(motionEnabled){
     GyroStreamWatchdog stream;if(!streams.TryGetValue(entry.Key,out stream)){stream=new GyroStreamWatchdog();streams[entry.Key]=stream;}
     GyroPointerFilter filter;motion.TryGetValue(entry.Key,out filter);
     long now=clock.ElapsedMilliseconds;
     if(stream.ShouldRestart(now,filter!=null)){
      bool available=SDL_GameControllerHasSensor(entry.Value,2)!=0;
      if(available){SDL_GameControllerSetSensorEnabled(entry.Value,2,0);available=SDL_GameControllerSetSensorEnabled(entry.Value,2,1)==0;}
      // Re-enabling an idle stream must not throw away a completed stationary
      // calibration. That made the pointer vanish whenever a good controller
      // spent long enough at exact zero between movements.
      if(!available)filter=null;else if(filter==null)filter=new GyroPointerFilter(rememberedMotionBias);motion[entry.Key]=filter;stream.Restarted(now);
     }
     MotionStatus=filter==null?"sensor unavailable; retrying":"calibrating";
     if(filter!=null){var rate=new float[3];ulong stamp;if(SDL_GameControllerGetSensorDataWithTimestamp(entry.Value,2,out stamp,rate,3)==0){
      stream.Observe(now,stamp,rate);filter.Update(rate,stamp);
      if(filter.Ready)rememberedMotionBias=filter.Calibration;
      if(!MotionReady&&filter.Ready&&stream.IsFresh(now)){MotionReady=true;GyroX=filter.DeltaX;GyroY=filter.DeltaY;MotionStatus="ready";}
      else if(!stream.IsFresh(now))MotionStatus="sensor stalled; recovering";
     }else MotionStatus="sensor read failed; retrying";}
    }
    uint buttons=0;for(int b=0;b<16;b++)if(SDL_GameControllerGetButton(entry.Value,b)!=0)buttons|=1u<<b;
    uint old;if(previous.TryGetValue(entry.Key,out old))Pressed|=buttons&~old;previous[entry.Key]=buttons;
    int sd=StereoDirection(SDL_GameControllerGetAxis(entry.Value,4),SDL_GameControllerGetAxis(entry.Value,5),buttons),last;long due;
    // A controller connected with the chord held must first release it.
    bool known=stereoDirection.TryGetValue(entry.Key,out last);stereoRepeatAt.TryGetValue(entry.Key,out due);
    if((!known||last==99)&&sd!=0)stereoDirection[entry.Key]=99;
    else {if(sd!=0&&(sd!=last||(sd!=3&&clock.ElapsedMilliseconds>=due))){StereoMove=sd;stereoRepeatAt[entry.Key]=clock.ElapsedMilliseconds+(sd!=last?400:150);}stereoDirection[entry.Key]=sd;}
    int x=SDL_GameControllerGetAxis(entry.Value,0),y=SDL_GameControllerGetAxis(entry.Value,1);
    int d=(buttons&(1u<<11))!=0?-5:(buttons&(1u<<12))!=0?5:(buttons&(1u<<13))!=0?-1:(buttons&(1u<<14))!=0?1:Math.Max(Math.Abs(x),Math.Abs(y))<16000?0:Math.Abs(x)>Math.Abs(y)?(x<0?-1:1):(y<0?-5:5);
    int prior;direction.TryGetValue(entry.Key,out prior);long next;repeatAt.TryGetValue(entry.Key,out next);
    if(d!=0&&(d!=prior||clock.ElapsedMilliseconds>=next)){Move=d;repeatAt[entry.Key]=clock.ElapsedMilliseconds+(d!=prior?350:130);}direction[entry.Key]=d;
    if(!both)armed.Add(entry.Key);else if(armed.Remove(entry.Key))exit=true;
   }
   if(motionEnabled){
    if(MotionReady)motionUnhealthySince=-1;
    else if(motionUnhealthySince<0)motionUnhealthySince=clock.ElapsedMilliseconds;
    else if(clock.ElapsedMilliseconds-motionUnhealthySince>=6000){
     // Toggling the sensor cannot recover a stale HID handle. Re-enumerate
     // the backend as well, only while the menu owns controller motion.
     Dispose();motionUnhealthySince=clock.ElapsedMilliseconds;MotionStatus="reopening controller connection";
    }
   }
   return exit;
  }catch(DllNotFoundException){unavailable=true;Status="SDL2 missing";return false;}catch(EntryPointNotFoundException){unavailable=true;Status="SDL2 incompatible";return false;}
 }
 public void Dispose(){foreach(var pad in pads.Values)SDL_GameControllerClose(pad);pads.Clear();motion.Clear();streams.Clear();previous.Clear();direction.Clear();repeatAt.Clear();Reset();MotionReady=false;GyroX=GyroY=0;if(initialized)SDL_QuitSubSystem(0x2000);initialized=false;}
}
