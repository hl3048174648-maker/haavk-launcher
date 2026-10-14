param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../../docs/demos'), [string]$FFmpeg = 'ffmpeg')
$ErrorActionPreference = 'Stop'
$package = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path -Parent $compiler
$wpf = Join-Path $framework 'WPF'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$encoder = (Get-Command $FFmpeg -ErrorAction Stop).Source
$recorder = Join-Path $OutputDirectory 'RecordDemo.exe'
$sources = @('LaunchCore.cs','VideoOptions.cs','VideoLauncher.cs','tests/RecordDemo.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:exe /main:RecordDemo ('/out:'+$recorder) ('/r:'+(Join-Path $wpf 'PresentationCore.dll')) ('/r:'+(Join-Path $wpf 'PresentationFramework.dll')) ('/r:'+(Join-Path $wpf 'WindowsBase.dll')) ('/r:'+(Join-Path $framework 'System.Xaml.dll')) @sources
if($LASTEXITCODE -ne 0) { throw 'Recorder build failed.' }
foreach($variant in @('01','02','03')) {
    & $recorder $variant $package (Join-Path $OutputDirectory ('HAAVK-'+$variant+'-demo.mp4')) $encoder
    if($LASTEXITCODE -ne 0) { throw ('Recording failed for '+$variant) }
}
