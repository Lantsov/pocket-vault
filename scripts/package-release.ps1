param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'PocketVault.App\PocketVault.App.csproj')
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid project version.' }
$output = Join-Path $projectRoot 'dist\releases'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$variants = @('self-contained', 'framework-dependent')
$names = @($variants | ForEach-Object { "PocketVault-$version-win-x64-$_.zip" })
foreach ($name in @($names) + @('SHA256SUMS.txt')) {
    if (Test-Path -LiteralPath (Join-Path $output $name)) { throw "Refusing to replace existing output: $name" }
}
# Use the exact runtime packs selected by restore, not whichever cache version is newest.
$assets = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'PocketVault.App\obj\project.assets.json') | ConvertFrom-Json -AsHashtable
$runtimeFiles = @{}
foreach ($pack in @('Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64')) {
    $dependencies = @($assets.project.frameworks.Values | ForEach-Object { $_.downloadDependencies } |
        Where-Object { $_.name -eq $pack } | Select-Object -ExpandProperty version -Unique)
    if ($dependencies.Count -ne 1 -or $dependencies[0] -notmatch '^\[([^,]+), \1\]$') {
        throw "Expected one exact restored runtime pack: $pack. Publish SelfContained first."
    }
    $packVersion = $Matches[1]
    $relative = $pack.ToLowerInvariant() + '/' + $packVersion
    $packRoot = $assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $relative } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $packRoot) { throw "Restored runtime pack not found: $pack" }
    $license = Get-ChildItem -LiteralPath $packRoot -File | Where-Object { $_.Name -match '^LICENSE(?:\.TXT)?$' }
    $notices = Get-ChildItem -LiteralPath $packRoot -File | Where-Object { $_.Name -match '^THIRD[-_]?PARTY[-_]?NOTICES(?:\.TXT)?$' }
    if (-not $license -or ($pack -eq 'Microsoft.NETCore.App.Runtime.win-x64' -and -not $notices)) {
        throw "Required upstream license/notices missing: $pack"
    }
    $runtimeFiles["$pack/$packVersion"] = @($license) + @($notices)
}
$staging = Join-Path $projectRoot ('dist\release-staging-' + [Guid]::NewGuid().ToString('N'))
foreach ($variant in $variants) {
    $published = Join-Path $projectRoot "dist\$variant"
    $exe = Join-Path $published 'PocketVault.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "Publish output missing: $variant" }
    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion.Split('+')[0]
    if ($productVersion -ne $version) { throw "Published EXE version mismatch: $variant" }
    $folder = Join-Path $staging $variant
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    Get-ChildItem -LiteralPath $published | Copy-Item -Destination $folder -Recurse
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $folder
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\THIRD_PARTY_NOTICES.md') -Destination $folder
    if ($variant -eq 'self-contained') {
        foreach ($pack in $runtimeFiles.Keys) {
            $noticeFolder = Join-Path $folder ('runtime-notices\' + $pack.Replace('/', '-'))
            New-Item -ItemType Directory -Path $noticeFolder -Force | Out-Null
            foreach ($file in $runtimeFiles[$pack]) { Copy-Item -LiteralPath $file.FullName -Destination $noticeFolder }
        }
    }
    $archive = Join-Path $output "PocketVault-$version-win-x64-$variant.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($folder, $archive, [IO.Compression.CompressionLevel]::Optimal, $false)
}
$sums = foreach ($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), ($sums -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Output "[OK] Two archives and SHA256SUMS.txt prepared in dist/releases (version $version). No release uploaded."
