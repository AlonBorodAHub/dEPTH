$ErrorActionPreference='Stop'
$dest=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $dest | Out-Null
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /reference:System.Web.Extensions.dll "/out:$dest\HubSessionGuard.exe" "$PSScriptRoot\HubSessionGuard.cs"
if($LASTEXITCODE -ne 0){throw 'Hub guard build failed'}
& $compiler /nologo /target:exe /main:HubSessionChecks /reference:System.Web.Extensions.dll "/out:$dest\HubSessionChecks.exe" "$PSScriptRoot\HubSessionGuard.cs" "$PSScriptRoot\HubSessionChecks.cs"
if($LASTEXITCODE -ne 0){throw 'Hub checks build failed'}
& "$dest\HubSessionChecks.exe"
if($LASTEXITCODE -ne 0){throw 'Hub checks failed'}
