# Compatible with Windows PowerShell 5.1. No authentication or extra modules needed.
[CmdletBinding()]
param(
    [string]$DestinationDirectory,
    [string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
# Resolve defaults in the script body: Windows PowerShell -File can evaluate
# parameter defaults before PSScriptRoot is available.
$defaultVideoDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\videos'))
if (-not $PSBoundParameters.ContainsKey('DestinationDirectory')) { $DestinationDirectory = $defaultVideoDirectory }
if (-not $PSBoundParameters.ContainsKey('ManifestPath')) { $ManifestPath = Join-Path $defaultVideoDirectory 'manifest.json' }

function Read-HaavkVideoManifest {
    param([string]$Path)
    $manifest = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $manifest -or $manifest.schemaVersion -ne 1 -or @($manifest.videos).Count -ne 3) {
        throw 'Invalid video manifest: expected schemaVersion 1 and three videos.'
    }
    $seen = @{}
    $entries = @(foreach ($entry in $manifest.videos) {
        $fileName = [string]$entry.fileName
        if ($fileName -cnotmatch '\AHAAVK_0[123]\.mp4\z' -or $seen.ContainsKey($fileName)) {
            throw 'Invalid video manifest: filenames must be unique HAAVK_01.mp4, HAAVK_02.mp4 and HAAVK_03.mp4.'
        }
        $seen[$fileName] = $true
        $uri = $null
        if (-not [Uri]::TryCreate([string]$entry.url, [UriKind]::Absolute, [ref]$uri) -or
            $uri.Scheme -ne 'https' -or $uri.UserInfo.Length -ne 0) {
            throw ('Invalid video manifest: HTTPS URL required for ' + $fileName)
        }
        $size = [long]0
        $sizeText = [string]$entry.sizeBytes
        if ($sizeText -notmatch '\A[1-9][0-9]*\z' -or -not [long]::TryParse($sizeText, [ref]$size)) {
            throw ('Invalid video manifest: positive integer sizeBytes required for ' + $fileName)
        }
        $hash = [string]$entry.sha256
        if ($hash -notmatch '\A[0-9a-fA-F]{64}\z') {
            throw ('Invalid video manifest: SHA-256 required for ' + $fileName)
        }
        [PSCustomObject]@{FileName=$fileName; Uri=$uri; SizeBytes=$size; SHA256=$hash}
    })
    return $entries
}

function Test-HaavkVideoFile {
    param([string]$Path, [long]$SizeBytes, [string]$SHA256)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $Path).Length -ne $SizeBytes) { return $false }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $SHA256
}

function Receive-HaavkVideoFile {
    param([Uri]$Uri, [string]$Path)
    $previousProtocol = [Net.ServicePointManager]::SecurityProtocol
    try {
        [Net.ServicePointManager]::SecurityProtocol = $previousProtocol -bor [Net.SecurityProtocolType]::Tls12
        # GitHub redirects release assets. Check every hop so transfers stay HTTPS.
        for ($redirect = 0; $redirect -le 10; $redirect++) {
            if ($Uri.Scheme -ne 'https' -or $Uri.UserInfo.Length -ne 0) { throw 'Download redirect must use HTTPS without credentials.' }
            $request = [Net.HttpWebRequest]::Create($Uri)
            $request.AllowAutoRedirect = $false
            $request.UserAgent = 'HAAVK-Launcher-Video-Download/1.0'
            $request.Timeout = 60000
            $request.ReadWriteTimeout = 300000
            $response = $null
            try {
                $response = $request.GetResponse()
                $status = [int]$response.StatusCode
                if (@(301,302,303,307,308) -contains $status) {
                    $location = $response.Headers['Location']
                    if ([string]::IsNullOrWhiteSpace($location)) { throw 'Download redirect has no Location header.' }
                    $Uri = New-Object Uri($Uri, $location)
                    continue
                }
                if ($status -ne 200) { throw ('Unexpected HTTP response: ' + $status) }
                $inputStream = $null
                $outputStream = $null
                try {
                    $inputStream = $response.GetResponseStream()
                    $outputStream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    $inputStream.CopyTo($outputStream)
                }
                finally {
                    if ($null -ne $outputStream) { $outputStream.Dispose() }
                    if ($null -ne $inputStream) { $inputStream.Dispose() }
                }
                return
            }
            finally { if ($null -ne $response) { $response.Close() } }
        }
        throw 'Too many HTTPS download redirects.'
    }
    finally { [Net.ServicePointManager]::SecurityProtocol = $previousProtocol }
}

function Invoke-HaavkVideoRestore {
    param([string]$DestinationDirectory, [string]$ManifestPath)
    if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) { throw 'DestinationDirectory must not be empty.' }
    # Validate every entry before creating or downloading any destination file.
    $entries = @(Read-HaavkVideoManifest -Path $ManifestPath)
    $destination = [IO.Path]::GetFullPath($DestinationDirectory)
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($entry in $entries) {
        $targetPath = Join-Path $destination $entry.FileName
        if (Test-HaavkVideoFile -Path $targetPath -SizeBytes $entry.SizeBytes -SHA256 $entry.SHA256) {
            [PSCustomObject]@{FileName=$entry.FileName; Status='Skipped'; Path=$targetPath}
            continue
        }
        # A unique sibling file keeps partial downloads separate and on the same volume.
        $partialPath = $targetPath + '.partial-' + [Guid]::NewGuid().ToString('N')
        try {
            Write-Host ('Downloading ' + $entry.FileName + ' (' + $entry.SizeBytes + ' bytes)...')
            Receive-HaavkVideoFile -Uri $entry.Uri -Path $partialPath
            if (-not (Test-HaavkVideoFile -Path $partialPath -SizeBytes $entry.SizeBytes -SHA256 $entry.SHA256)) {
                throw ('Download verification failed (size or SHA-256): ' + $entry.FileName)
            }
            if ([IO.File]::Exists($targetPath)) {
                # PowerShell 5.1 converts ordinary $null to an invalid empty backup path.
                [IO.File]::Replace($partialPath, $targetPath, [NullString]::Value)
            }
            else { [IO.File]::Move($partialPath, $targetPath) }
            [PSCustomObject]@{FileName=$entry.FileName; Status='Downloaded'; Path=$targetPath}
        }
        finally {
            if (Test-Path -LiteralPath $partialPath -PathType Leaf) { Remove-Item -LiteralPath $partialPath -Force }
        }
    }
}

# Dot-sourcing exposes the functions for offline verification without starting downloads.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-HaavkVideoRestore -DestinationDirectory $DestinationDirectory -ManifestPath $ManifestPath
}
