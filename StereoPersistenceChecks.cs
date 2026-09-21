using System;using System.Collections.Generic;
class StereoPersistenceChecks {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main(){
  uint shoulders=(1u<<9)|(1u<<10);
  Check(ControllerInput.StereoDirection(32767,32767,shoulders)==3,"Four-button reset recognized");
  Check(ControllerInput.StereoDirection(32767,0,shoulders)==0,"Both triggers required");
  Check(ControllerInput.StereoDirection(32767,32767,1u<<9)==0,"Both shoulders required");
  Check(ControllerInput.StereoDirection(32767,32767,shoulders|(1u<<11))==3,"Reset takes precedence over depth");
  var original=new StereoProfile{OriginalDepth=25,OriginalConvergence=1,Depth=12,Convergence=2,Backend="flycast"};
  var store=new Dictionary<string,StereoProfile>{{"game1",original},{"game2",new StereoProfile{Depth=40}}};
  var loaded=Storage.Json.Deserialize<Dictionary<string,StereoProfile>>(Storage.Json.Serialize(store));
  Check(loaded["game1"].Depth==12&&loaded["game1"].Convergence==2,"Latest values survive reload");
  loaded["game1"].Reset();Check(loaded["game1"].Depth==25&&loaded["game1"].Convergence==1,"Reset restores separate original values");
  Check(loaded["game2"].Depth==40,"Games stay independent");
  Check(StereoProfiles.AzaharStep(255,1)==255&&StereoProfiles.AzaharStep(0,-1)==0,"Depth clamps at native limits");
  Check(StereoProfiles.AzaharStep(103,-1)==100&&StereoProfiles.AzaharStep(103,1)==105,"Native rounding matches");
  Check(StereoProfiles.Number("[Constants]\n$depth_saved_convergence = 2.125\n","$depth_saved_convergence",0)==2.125,"Read exact native saved values");
  Check(StereoProfiles.SetNumber("factor_3d=100\nother=1\n","factor_3d",80).Contains("other=1"),"Preserve other settings");
  Console.WriteLine("PASS: reset chord, immutable original values, per-game roundtrip, native bounds, saved-value parsing.");
 }
}
