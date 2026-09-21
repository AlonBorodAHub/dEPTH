using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

public partial class DepthWindow {
 long lastTabletCheck=-1000;
 IntPtr tabletWindow;int tabletPhase;long tabletActionAt;
 Rectangle tabletBounds;
 int tabletMoveAttempts;
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public WindowRect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 delegate bool MonitorVisitor(IntPtr monitor,IntPtr dc,IntPtr rect,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorVisitor visitor,IntPtr data);
 static Rectangle MonitorBounds(IntPtr monitor){var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};return GetMonitorInfo(monitor,ref info)?Rectangle.FromLTRB(info.Monitor.Left,info.Monitor.Top,info.Monitor.Right,info.Monitor.Bottom):Rectangle.Empty;}
 public static bool IsAzaharSecondaryTitle(string title){return title!=null&&title.EndsWith(" | Secondary Window",StringComparison.Ordinal);}
 public static bool MatchesDisplay(Rectangle window,Rectangle display){return Math.Abs(window.Left-display.Left)<=3&&Math.Abs(window.Top-display.Top)<=3&&Math.Abs(window.Right-display.Right)<=3&&Math.Abs(window.Bottom-display.Bottom)<=3;}
 static void TabletFullscreenKey(IntPtr window){PostMessage(window,0x0100,new IntPtr(0x7A),new IntPtr(0x00570001));PostMessage(window,0x0101,new IntPtr(0x7A),new IntPtr(unchecked((int)0xC0570001)));}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowStyle(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)] static extern int SetWindowStyle(IntPtr window,int index,int value);

 [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
 // Prefer the display below the game screen, with the closest horizontal center.
 // A disconnected tablet must never move the touch window onto the 3D display.
 public static int LowerDisplay(Rectangle main,Rectangle[] displays){
  int best=-1;double score=double.MaxValue;
  for(int i=0;i<displays.Length;i++){
   Rectangle r=displays[i];if(r==main||r.Top<main.Bottom-8)continue;
   double distance=(r.Top-main.Bottom)*4+Math.Abs((r.Left+r.Width/2.0)-(main.Left+main.Width/2.0));
   if(distance<score){score=distance;best=i;}
  }
  return best;
 }
 void PlaceTabletScreen(){
  if(activeGame==null||!string.Equals(Path.GetFileNameWithoutExtension(activeGame.Executable),"azahar",StringComparison.OrdinalIgnoreCase))return;
  if(handoffClock.ElapsedMilliseconds-lastTabletCheck<500)return;lastTabletCheck=handoffClock.ElapsedMilliseconds;
  // Qt is per-monitor DPI aware. Use the same physical coordinates for monitor
  // enumeration, movement and verification, rather than cached WinForms bounds.
  IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try{PlaceTabletScreenPhysical();}finally{if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous);}
 }
 void PlaceTabletScreenPhysical(){
  var displays=new System.Collections.Generic.List<Rectangle>();
  EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(monitor,dc,rect,data)=>{displays.Add(MonitorBounds(monitor));return true;},IntPtr.Zero);
  int target=LowerDisplay(MonitorBounds(MonitorFromWindow(Handle,2)),displays.ToArray());if(target<0)return;
  Rectangle bounds=displays[target];
  IntPtr candidate=IntPtr.Zero;
  EnumWindows((window,state)=>{
   if(!IsWindowVisible(window))return true;
   var title=new StringBuilder(512);GetWindowText(window,title,title.Capacity);
   if(IsAzaharSecondaryTitle(title.ToString())&&BelongsToGame(window)){candidate=window;return false;}return true;
  },IntPtr.Zero);
  if(candidate==IntPtr.Zero)return;
  if(candidate!=tabletWindow||tabletBounds!=bounds){tabletWindow=candidate;tabletBounds=bounds;tabletPhase=0;tabletMoveAttempts=0;}
  long now=handoffClock.ElapsedMilliseconds;
  if(tabletPhase==0){
   WindowRect current;if(!GetWindowRect(candidate,out current))return;
   Rectangle currentBounds=Rectangle.FromLTRB(current.Left,current.Top,current.Right,current.Bottom);
   if(MatchesDisplay(currentBounds,bounds)){tabletPhase=4;return;}
   // A saved fullscreen secondary must leave Qt fullscreen before being moved.
   if(displays.Any(display=>MatchesDisplay(currentBounds,display))){SetForegroundWindow(candidate);tabletPhase=5;tabletActionAt=now+500;return;}
  }
  if(tabletPhase==5&&now>=tabletActionAt){
   if(GetForegroundWindow()!=candidate){SetForegroundWindow(candidate);return;}
   TabletFullscreenKey(candidate);tabletPhase=6;tabletActionAt=now+750;return;
  }
  if(tabletPhase==6){if(now<tabletActionAt)return;tabletPhase=0;}
  if(tabletPhase==0){
   // A real Qt fullscreen transition resizes the embedded renderer and handles DPI.
   ShowWindow(candidate,9);
   int style=GetWindowStyle(candidate,-16);SetWindowStyle(candidate,-16,style|0x00CF0000);
   SetWindowPos(candidate,IntPtr.Zero,bounds.X+40,bounds.Y+40,bounds.Width-80,bounds.Height-80,0x0034);
   tabletPhase=1;tabletActionAt=now+500;return;
  }
  if(tabletPhase==1&&now>=tabletActionAt){
   WindowRect placed;GetWindowRect(candidate,out placed);
   if(Math.Abs(placed.Left-(bounds.Left+40))>8||Math.Abs(placed.Top-(bounds.Top+40))>8){
    // Qt may rebase the first cross-DPI move while handling WM_DPICHANGED.
    // Reapply after that event has settled before requesting fullscreen.
    if(++tabletMoveAttempts>3){tabletPhase=4;try{File.WriteAllText(Path.Combine(Storage.Folder,"tablet-display.txt"),"Azahar touch placement failed before fullscreen; target "+bounds+"; actual "+Rectangle.FromLTRB(placed.Left,placed.Top,placed.Right,placed.Bottom));}catch{}return;}
    ShowWindow(candidate,9);
    SetWindowPos(candidate,IntPtr.Zero,bounds.X+40,bounds.Y+40,bounds.Width-80,bounds.Height-80,0x0014);
    tabletActionAt=now+500;return;
   }
   // Target only the secondary window; never send a system-wide shortcut.
   SetForegroundWindow(candidate);tabletPhase=2;tabletActionAt=now+250;return;
  }
  if(tabletPhase==2&&now>=tabletActionAt){
   if(GetForegroundWindow()!=candidate){SetForegroundWindow(candidate);tabletActionAt=now+500;return;}
   TabletFullscreenKey(candidate);
   tabletPhase=3;tabletActionAt=now+500;return;
  }
  if(tabletPhase==3&&now>=tabletActionAt){
   WindowRect rect;if(!GetWindowRect(candidate,out rect))return;
   bool full=MatchesDisplay(Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom),bounds);
   // Do not toggle repeatedly: F11 is a toggle, not an idempotent command.
   tabletPhase=4;if(gameWindow!=IntPtr.Zero)SetForegroundWindow(gameWindow);
   try{File.WriteAllText(Path.Combine(Storage.Folder,"tablet-display.txt"),"Azahar secondary "+candidate+" target "+bounds+"; actual "+Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom)+"; fullscreen bounds verified: "+full);}catch{}
  }
 }
}
