using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
public partial class DepthWindow {
 [StructLayout(LayoutKind.Sequential)] struct StereoMouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct StereoKeyboard {public ushort key,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] struct StereoUnion {[FieldOffset(0)]public StereoKeyboard keyboard;[FieldOffset(0)]public StereoMouse mouse;}
 [StructLayout(LayoutKind.Sequential)] struct StereoInput {public uint type;public StereoUnion value;}
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,StereoInput[] inputs,int size);
 int stereoHeldKey;long stereoReleaseAt;
 public static int StereoShortcut(string executable,int move){
  string name=Path.GetFileNameWithoutExtension(executable).ToLowerInvariant();
  if(name=="flycast")return move==-1?0x80:move==1?0x81:move==-2?0x82:move==2?0x83:0;
  if(name=="azahar")return move==-2?0x82:move==2?0x83:0;
  return 0; // Dolphin owns its native controller chords.
 }
 static bool SendStereoKey(int key,bool up){var input=new StereoInput{type=1,value=new StereoUnion{keyboard=new StereoKeyboard{key=(ushort)key,flags=up?2u:0u}}};return SendInput(1,new[]{input},Marshal.SizeOf(typeof(StereoInput)))==1;}
 void ReleaseStereoKey(){if(stereoHeldKey!=0){SendStereoKey(stereoHeldKey,true);stereoHeldKey=0;}}
 bool StereoForeground(){IntPtr window=GetForegroundWindow();if(window==IntPtr.Zero)return false;if(BelongsToGame(window))return true;uint pid;GetWindowThreadProcessId(window,out pid);try{using(var p=Process.GetProcessById((int)pid))return p.ProcessName=="Odyssey3DPlayer";}catch{return false;}}
 void HandleStereoButtons(){
  bool ready=activeGame!=null&&playing&&closingSince<0&&IsWindow(gameWindow)&&BelongsToGame(gameWindow);
  if(stereoHeldKey!=0&&(!ready||frameClock.ElapsedMilliseconds>=stereoReleaseAt))ReleaseStereoKey();
  if(!ready||!StereoForeground())return;
  if(controllerInput.StereoMove==3){BeginStereoReset();return;}
  int move=controllerInput.StereoMove;
  if(resettingStereo){
   if(stereoProfile==null||stereoProfile.Depth==stereoProfile.OriginalDepth){resettingStereo=false;return;}
   if(stereoHeldKey!=0||frameClock.ElapsedMilliseconds<nextStereoReset)return;
   move=stereoProfile.Depth>stereoProfile.OriginalDepth?-2:2;nextStereoReset=frameClock.ElapsedMilliseconds+150;
  }
  int key=StereoShortcut(activeGame.Executable,move);if(key==0)return;
  ReleaseStereoKey();if(SendStereoKey(key,false)){stereoHeldKey=key;stereoReleaseAt=frameClock.ElapsedMilliseconds+70;SaveAzaharAdjustment(move);}
 }
}
