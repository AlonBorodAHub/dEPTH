using System;
using System.Collections.Generic;
public static class HubSessionChecks {
 public static void Main(){
  var s=new Dictionary<string,object>{{"CurrOverlayKey","Ctrl+Shift+1"},{"Curr3DModeKey","Ctrl+Shift+2"},{"Curr3DDepthNegKey","Ctrl+Shift+3"},{"Curr3DDepthPosKey","Ctrl+Shift+4"},{"Curr3DPopoutNegKey","Ctrl+Shift+5"},{"Curr3DPopoutPosKey","Ctrl+Shift+6"},{"Language","en-US"},{"CursorType",2},{"IsConvertVideoTo3D",0},{"IsAutoGameScan",0},{"IsAutoRun",1},{"IsAutoConvert",0},{"IsStandardVideo",1}};
  string active=HubSessionGuard.Message(s,true,1,0),idle=HubSessionGuard.Message(s,false,1,0);
  if(!active.EndsWith("/en-US/2/1/0/1/1/0"))throw new Exception("Active SBS settings incorrect");
  if(!idle.EndsWith("/en-US/2/0/0/1/0/1"))throw new Exception("Restoration settings incorrect");
  if(Convert.ToInt32(s["IsAutoConvert"])!=0)throw new Exception("Source settings mutated");
  s["Language"]="en-US/1";bool rejected=false;try{HubSessionGuard.Message(s,true,1,1);}catch(System.IO.InvalidDataException){rejected=true;}
  if(!rejected)throw new Exception("Delimiter not rejected");
  Console.WriteLine("PASS: automatic SBS, desktop restoration, other settings preserved, source unchanged, malformed values rejected.");
 }
}
