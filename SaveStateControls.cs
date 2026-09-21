using System;
using System.IO;

public partial class DepthWindow {
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);
 long lastSaveStateButton=-2000;
 public static int SaveStateKey(string executable,uint pressed){
  if(!string.Equals(Path.GetFileNameWithoutExtension(executable),"rpcs3",StringComparison.OrdinalIgnoreCase))return 0;
  uint state=pressed&((1u<<5)|(1u<<15));
  return state==(1u<<5)?0x7C:state==(1u<<15)?0x7D:0;
 }
 void HandleSaveStateButtons(){
  // Other emulators own native mappings; never send them a duplicate command.
  if(activeGame==null||!playing||closingSince>=0||!IsWindow(gameWindow)||!BelongsToGame(gameWindow))return;
  int key=SaveStateKey(activeGame.Executable,controllerInput.Pressed);
  if(key==0||frameClock.ElapsedMilliseconds-lastSaveStateButton<1500)return;
  lastSaveStateButton=frameClock.ElapsedMilliseconds;
  int messageBits=1|((int)MapVirtualKey((uint)key,0)<<16);
  PostMessage(gameWindow,0x0100,new IntPtr(key),new IntPtr(messageBits));
  PostMessage(gameWindow,0x0101,new IntPtr(key),new IntPtr(unchecked(messageBits|(int)0xC0000000)));
 }
}
