using System;
class StereoShortcutChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  foreach(int bit in new[]{11,12,13,14}){Check(ControllerInput.StereoDirection(0,32767,1u<<bit)==0,"Both triggers required");Check(ControllerInput.StereoDirection(32767,0,1u<<bit)==0,"Both triggers required");}
  Check(ControllerInput.StereoDirection(32767,32767,1u<<11)==2,"Up increases depth");
  Check(ControllerInput.StereoDirection(32767,32767,1u<<12)==-2,"Down decreases depth");
  Check(ControllerInput.StereoDirection(32767,32767,1u<<13)==-1,"Left decreases convergence");
  Check(ControllerInput.StereoDirection(32767,32767,1u<<14)==1,"Right increases convergence");
  Check(ControllerInput.StereoDirection(32767,32767,(1u<<11)|(1u<<13))==0,"Ambiguous diagonals ignored");
  Check(DepthWindow.StereoShortcut("flycast.exe",-1)==0x80,"Dreamcast convergence down");
  Check(DepthWindow.StereoShortcut("flycast.exe",2)==0x83,"Dreamcast depth up");
  Check(DepthWindow.StereoShortcut("azahar.exe",2)==0x83,"Azahar depth up");
  Check(DepthWindow.StereoShortcut("azahar.exe",1)==0,"No unsupported convergence command");
  Check(DepthWindow.StereoShortcut("Dolphin.exe",2)==0,"Native Dolphin mapping not duplicated");
  Check(DepthWindow.StereoShortcut("rpcs3.exe",2)==0,"No unsupported command");
  Console.WriteLine("PASS: trigger gating, D-pad directions, diagonal rejection, emulator routing and unsupported commands.");
 }
}
