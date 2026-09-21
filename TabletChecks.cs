using System;
using System.Drawing;
class TabletChecks {
 static void Check(bool result,string message){if(!result)throw new Exception(message);}
 static void Main(){
  var main=new Rectangle(0,0,3840,2160);var tablet=new Rectangle(851,2160,2240,1400);
  Check(DepthWindow.LowerDisplay(main,new[]{main,tablet})==1,"Choose tablet below Odyssey");
  Check(DepthWindow.LowerDisplay(main,new[]{main})==-1,"No tablet must leave main display alone");
  Check(DepthWindow.LowerDisplay(main,new[]{new Rectangle(3840,0,1920,1080),main,tablet})==2,"Ignore side display");
  Check(DepthWindow.LowerDisplay(main,new[]{main,new Rectangle(-2240,2160,2240,1400),tablet})==2,"Choose centered lower display");
  Check(DepthWindow.IsAzaharSecondaryTitle("Azahar 2126.1.2 | Game | Secondary Window"),"Exclude touch window from game handoff");
  Check(!DepthWindow.IsAzaharSecondaryTitle("Azahar 2126.1.2 | Game | Primary Window"),"Allow primary game window");
  Check(DepthWindow.MatchesDisplay(tablet,tablet),"Exact fullscreen tablet bounds");
  Check(!DepthWindow.MatchesDisplay(new Rectangle(1277,3240,3360,2100),tablet),"Reject DPI-scaled monitor bounds");
  Check(!DepthWindow.MatchesDisplay(new Rectangle(0,0,5000,5000),tablet),"A spanning window is not tablet fullscreen");
  Console.WriteLine("PASS: tablet selection, disconnected/multiple monitors, primary/secondary window identification, and exact fullscreen bounds across DPI settings.");
 }
}
