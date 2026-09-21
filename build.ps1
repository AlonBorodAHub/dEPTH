$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
New-Item -ItemType Directory -Force -Path (Join-Path $root 'bin') | Out-Null
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:winexe /optimize+ /win32icon:"$root\assets\depth.ico" "/resource:$root\assets\depth.ico,Depth.AppIcon" /out:"$root\bin\Depth.exe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$root\Depth.cs" "$root\PublicSetup.cs" "$root\Rendering.cs" "$root\FrameTiming.cs" "$root\HeadTracking.cs" "$root\GpuRenderer.cs" "$root\Transitions.cs" "$root\ControllerInput.cs" "$root\SaveStateControls.cs" "$root\StereoControls.cs" "$root\StereoPersistence.cs" "$root\Navigation.cs" "$root\Polish.cs" "$root\Arrival.cs" "$root\TabletDisplay.cs" "$root\VectorTitle.cs" "$root\Companion.cs" "$root\ConsoleModels.cs" "$root\DiscSystems.cs" "$root\ImportedModels.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$modelRoot=Join-Path $root 'bin\data\models'
New-Item -ItemType Directory -Force -Path $modelRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'assets\models\model.vert'),(Join-Path $root 'assets\models\model.frag') -Destination $modelRoot -Force
Copy-Item -LiteralPath (Join-Path $root 'assets\models\CREDITS.md') -Destination $modelRoot -Force
$selection=Get-Content (Join-Path $root 'assets\models\selection.json') -Raw | ConvertFrom-Json
foreach($property in $selection.PSObject.Properties){
 $name=$property.Name.Replace(' ','-');$source=Join-Path $root ('assets\models\'+$name);$destination=Join-Path $modelRoot $name
 New-Item -ItemType Directory -Force -Path $destination | Out-Null
 Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.mesh','.png' -or $_.Name -in 'materials.json','source.json','LICENSE.txt' } | Copy-Item -Destination $destination -Force
}
Write-Output "Built $root\bin\Depth.exe with imported models and credits"

