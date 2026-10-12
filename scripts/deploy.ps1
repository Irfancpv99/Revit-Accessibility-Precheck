<#
Builds the add-in and copies it to the Revit 2026 per-user add-in folder.
Run with Revit CLOSED (a loaded DLL is locked). Never touches any model file.
Usage:  powershell -File scripts\deploy.ps1 [-Configuration Debug|Release] [-Dotnet <path to dotnet.exe>]
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

if (Get-Process Revit -ErrorAction SilentlyContinue) {
    throw "Revit is running. Close it first, then run this script again."
}

& $Dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

$out = Join-Path $repo "src\RevitAccessibility.Revit\bin\$Configuration\net10.0-windows"
New-Item -ItemType Directory -Force $target | Out-Null

foreach ($f in "RevitAccessibility.Revit.dll","RevitAccessibility.Revit.pdb","RevitAccessibility.Revit.deps.json",
               "RevitAccessibility.Core.dll","RevitAccessibility.Core.pdb","rule-register.csv") {
    Copy-Item (Join-Path $out $f) $target -Force
}
Copy-Item (Join-Path $out "RevitAccessibility.addin") $addins -Force

Write-Host "Deployed to $target"
Write-Host "Manifest:   $(Join-Path $addins 'RevitAccessibility.addin')"
Write-Host "Start Revit, open a COPY of a test model, then Add-Ins > External Tools > Accessibility Pre-Check."
