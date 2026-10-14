$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework = Split-Path -Parent $compiler
$wpf = Join-Path $framework 'WPF'
$destination = Split-Path -Parent $PSScriptRoot
$parameters = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu',
    ('/out:' + (Join-Path $destination 'HAAVK-Launch.exe')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $destination 'assets/launch.ico')),
    ('/r:' + (Join-Path $wpf 'PresentationCore.dll')),
    ('/r:' + (Join-Path $wpf 'PresentationFramework.dll')),
    ('/r:' + (Join-Path $wpf 'WindowsBase.dll')),
    ('/r:' + (Join-Path $framework 'System.Xaml.dll')),
    (Join-Path $PSScriptRoot 'LaunchCore.cs'),
    (Join-Path $PSScriptRoot 'VideoOptions.cs'),
    (Join-Path $PSScriptRoot 'VideoLauncher.cs'))
& $compiler @parameters
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output ('Built: ' + (Join-Path $destination 'HAAVK-Launch.exe'))
