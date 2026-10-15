using System;
class AzaharLaunchChecks {
 static void Main(){
  string original="[UI]\r\nfullscreen\\default=true\r\nfullscreen=false\r\nconfirmClose\\default=true\r\nconfirmClose=true\r\n[Layout]\r\nlayout_option=4\r\n[Controls]\r\nprofile=keep\r\n";
  string prepared=StereoProfiles.PrepareAzaharLaunch(original);
  if(!prepared.Contains("fullscreen\\default=false")||!prepared.Contains("fullscreen=true")||!prepared.Contains("confirmClose\\default=false")||!prepared.Contains("confirmClose=false"))throw new Exception("Launch settings not enforced");
  if(!prepared.Contains("layout_option=4")||!prepared.Contains("profile=keep"))throw new Exception("User configuration changed");
  if(StereoProfiles.PrepareAzaharLaunch(prepared)!=prepared)throw new Exception("Not idempotent");
  string missing=StereoProfiles.PrepareAzaharLaunch("[UI]\r\n[Layout]\r\nlayout_option=4\r\n");
  if(!missing.Contains("fullscreen=true")||!missing.Contains("fullscreen\\default=false"))throw new Exception("Missing settings not added");
  Console.WriteLine("PASS: Azahar fullscreen/default flags, close confirmation, missing keys, idempotence, and user configuration preservation.");
 }
}