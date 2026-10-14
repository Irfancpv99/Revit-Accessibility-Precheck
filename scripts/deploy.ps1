<#
Builds the add-in and copies it to the Revit 2026 per-user add-in folder.
Never touches any model file. Close Revit first: a loaded DLL is locked and cannot be replaced.
Usage:  .\scripts\deploy.ps1 [-Configuration Debug|Release] [-Dotnet <path to dotnet.exe>]
The add-in targets net10.0-windows, so -Dotnet must point at a .NET 10 SDK.
#>
param(
    [string]$Configuration = "Release",
    [string]$Dotnet = "dotnet"
)
$ErrorActionPreference = "Stop"

$repo    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "src\RevitAccessibility.Revit\RevitAccessibility.Revit.csproj"
$addins  = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2026"
$target  = Join-Path $addins "RevitAccessibility"

$running = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Warning ("Revit is running (PID " + (($running | ForEach-Object Id) -join ", ") + "). " +
                   "If the add-in was loaded in it, the copy below will fail. Close Revit and run this again.")
}

& $Dotnet build $project -c $Configuration --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$out = Join-Path $repo "src\RevitAccessibility.Revit\bin\$Configuration\net10.0-windows"
New-Item -ItemType Directory -Force $target | Out-Null

$files = "RevitAccessibility.Revit.dll","RevitAccessibility.Revit.pdb","RevitAccessibility.Revit.deps.json",
         "RevitAccessibility.Core.dll","RevitAccessibility.Core.pdb","rule-register.csv"
foreach ($f in $files) {
    $src = Join-Path $out $f
    if (-not (Test-Path $src)) { throw "Build output is missing $f ($src)." }
    try {
        Copy-Item $src $target -Force
    }
    catch [System.IO.IOException] {
        throw "Cannot overwrite $f because it is in use. Close Revit (check Task Manager > Details for Revit.exe) and run again."
    }
}
Copy-Item (Join-Path $out "RevitAccessibility.addin") $addins -Force

Write-Host "Deployed to $target"
Write-Host "Manifest:   $(Join-Path $addins 'RevitAccessibility.addin')"
Write-Host "Config:     $(Join-Path $env:APPDATA 'RevitAccessibility\config.json')   Log: $(Join-Path $env:APPDATA 'RevitAccessibility\log.txt')"
Write-Host "Start Revit, open a COPY of a test model, then Add-Ins > External Tools > Accessibility Pre-Check."
