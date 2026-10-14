param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'test-results'), [switch]$SkipPlayback)
$ErrorActionPreference = 'Stop'
$package = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path -Parent $compiler
$wpf = Join-Path $framework 'WPF'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
foreach($name in @('VideoOptionsTests','VideoControlsTest','PlaybackTest')) {
    $sources = @('LaunchCore.cs','VideoOptions.cs','VideoLauncher.cs',('tests/'+$name+'.cs')) | ForEach-Object { Join-Path $PSScriptRoot $_ }
    & $compiler /nologo /target:exe ('/main:'+$name) ('/out:'+(Join-Path $OutputDirectory ($name+'.exe'))) ('/r:'+(Join-Path $wpf 'PresentationCore.dll')) ('/r:'+(Join-Path $wpf 'PresentationFramework.dll')) ('/r:'+(Join-Path $wpf 'WindowsBase.dll')) ('/r:'+(Join-Path $framework 'System.Xaml.dll')) @sources
    if($LASTEXITCODE -ne 0) { throw ('Build failed: '+$name) }
}
& (Join-Path $OutputDirectory 'VideoOptionsTests.exe')
if($LASTEXITCODE -ne 0) { throw 'Option tests failed.' }
& (Join-Path $OutputDirectory 'VideoControlsTest.exe') $package (Join-Path $OutputDirectory ('fixtures-'+[Guid]::NewGuid().ToString('N')))
if($LASTEXITCODE -ne 0) { throw 'Control tests failed.' }
if(!$SkipPlayback) {
    foreach($variant in @('01','02','03')) {
        & (Join-Path $OutputDirectory 'PlaybackTest.exe') $variant $package (Join-Path $OutputDirectory ('playback-'+$variant+'.png'))
        if($LASTEXITCODE -ne 0) { throw ('Playback failed: '+$variant) }
    }
}
