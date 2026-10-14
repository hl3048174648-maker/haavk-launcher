param([string]$PreviewPath)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageRoot = Split-Path -Parent $sourceRoot
$assetsRoot = Join-Path $packageRoot 'assets'
if ([string]::IsNullOrWhiteSpace($PreviewPath)) {
    $PreviewPath = Join-Path (Split-Path -Parent $packageRoot) 'docs\icons-preview.png'
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$generator = Join-Path $sourceRoot 'IconGenerator.dll'
& $compiler /nologo /warnaserror+ /langversion:5 /target:library /reference:System.Drawing.dll "/out:$generator" (Join-Path $sourceRoot 'IconGenerator.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon generator compilation failed.' }
Add-Type -AssemblyName System.Drawing
$drawingAssembly = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($generator))
$drawingMethod = $drawingAssembly.GetType('IconGenerator').GetMethod('Generate')
$drawingArguments = [string[]]@($assetsRoot, $PreviewPath)
$drawingResult = $drawingMethod.Invoke($null, [object[]]@(,$drawingArguments))
if ($drawingResult -ne 0) { throw 'Icon generation or validation failed.' }
