param([ValidateSet('Install','Remove')][string]$Mode='Install',[string]$AppPath='', [string]$ExpectedUser='')
$ErrorActionPreference='Stop'
try {
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 if(!$ExpectedUser){$ExpectedUser=$identity.User.Value}
 if($identity.User.Value -ne $ExpectedUser){throw 'Run this setup from an administrator account used to play dEPTH. Elevating as a different user is not supported.'}
 if(!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
  $invoke="& '"+$PSCommandPath.Replace("'","''")+"' -Mode '$Mode' -AppPath '"+$AppPath.Replace("'","''")+"' -ExpectedUser '$ExpectedUser'"
  $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($invoke))
  $child=Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-EncodedCommand',$encoded)
  exit $child.ExitCode
 }
 $taskName='dEPTH Hub Session'
 $destination=Join-Path $env:ProgramFiles 'dEPTH'
 if($Mode -eq 'Remove'){
  if(Get-Process Depth -ErrorAction SilentlyContinue){throw 'Close dEPTH and its games before removing the helper.'}
  Start-Sleep -Seconds 2
  $settings=Join-Path $env:USERPROFILE 'AppData\LocalLow\Samsung\Odyssey3DHubUI\SettingsData\SettingsInfo.json'
  if(Test-Path $settings){if((Get-Content $settings -Raw|ConvertFrom-Json).Settings.IsAutoConvert -ne 0){throw 'Restore popup mode in Hub first, then run removal again.'}}
  Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
  Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
  Write-Host 'Automatic Hub switching disabled. Helper files have been retained.'
 }else{
  if(!$AppPath){$AppPath=Join-Path $PSScriptRoot 'Depth.exe';if(!(Test-Path $AppPath)){$AppPath=Join-Path $PSScriptRoot 'bin\Depth.exe'}}
  $AppPath=(Resolve-Path -LiteralPath $AppPath).Path
  if([IO.Path]::GetFileName($AppPath) -ne 'Depth.exe'){throw 'Select the Depth.exe application.'}
  $binary=Join-Path $PSScriptRoot 'HubSessionGuard.exe';if(!(Test-Path $binary)){$binary=Join-Path $PSScriptRoot 'bin\HubSessionGuard.exe'}
  if(!(Test-Path $binary)){throw 'Build the helper with build-hub-session.ps1 first.'}
  $settings=Join-Path $env:USERPROFILE 'AppData\LocalLow\Samsung\Odyssey3DHubUI\SettingsData\SettingsInfo.json'
  if(!(Test-Path $settings)){throw 'Install and run Samsung Odyssey 3D Hub before enabling this helper.'}
  $paths=@($AppPath)
  $config=Join-Path $destination 'HubSessionPaths.json'
  if(Test-Path $config){$paths+=@(Get-Content $config -Raw|ConvertFrom-Json)}
  $paths=@($paths|Select-Object -Unique)
  Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
  foreach($p in Get-Process HubSessionGuard -ErrorAction SilentlyContinue){if($p.MainModule.FileName -eq (Join-Path $destination 'HubSessionGuard.exe')){Stop-Process -Id $p.Id -Force;$p.WaitForExit(5000)|Out-Null}}
  New-Item -ItemType Directory -Force $destination | Out-Null
  Copy-Item -LiteralPath $binary -Destination (Join-Path $destination 'HubSessionGuard.exe') -Force
  ConvertTo-Json -InputObject $paths | Set-Content -LiteralPath $config
  $action=New-ScheduledTaskAction -Execute (Join-Path $destination 'HubSessionGuard.exe') -WorkingDirectory $destination
  $trigger=New-ScheduledTaskTrigger -AtLogOn -User $identity.Name
  $principal=New-ScheduledTaskPrincipal -UserId $identity.Name -LogonType Interactive -RunLevel Highest
  $options=New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
  Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $options -Description 'Automatic SBS during dEPTH; normal conversion popup after exit.' -Force | Out-Null
  Start-ScheduledTask -TaskName $taskName
  Write-Host 'Hub switching enabled. Run setup again if you move dEPTH to another folder.'
 }
}catch{Write-Host ('Setup failed: '+$_.Exception.Message) -ForegroundColor Red;Read-Host 'Press Enter to close';exit 1}
