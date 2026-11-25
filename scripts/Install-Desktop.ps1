param([string]$Dotnet = 'dotnet')

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$revision = git -C $repo rev-parse --short=12 HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not determine the source revision.' }
$changes = git -C $repo status --porcelain -- src tests
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit the desktop source and tests before installing a build.' }

$temp = Join-Path $repo ('src\DinamicScreenSound\bin\publish-' + [guid]::NewGuid().ToString('N'))
$project = Join-Path $repo 'src\DinamicScreenSound\DinamicScreenSound.csproj'
$tests = Join-Path $repo 'tests\DinamicScreenSound.Tests.csproj'
& $Dotnet run --project $tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'The routing tests failed.' }

& $Dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:DesktopBuild=true -p:PublishSingleFile=true -o $temp
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the desktop executable.' }

$changes = git -C $repo status --porcelain -- src tests
if ($LASTEXITCODE -ne 0 -or $changes -or (git -C $repo rev-parse --short=12 HEAD) -ne $revision) {
    throw 'The source changed during the build. Run the installer again.'
}

$candidate = Join-Path $temp 'DinamicScreenSound.exe'
$hash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
$root = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DinamicScreenSound'
$release = Join-Path $root ('releases\' + $revision + '-' + $hash.Substring(0, 12).ToLowerInvariant())
New-Item -Path $release -ItemType Directory -Force | Out-Null
$exe = Join-Path $release 'DinamicScreenSound.exe'
if (!(Test-Path -LiteralPath $exe)) {
    $copy = Join-Path $release ([guid]::NewGuid().ToString('N') + '.tmp')
    try {
        Copy-Item -LiteralPath $candidate -Destination $copy
        try { [IO.File]::Move($copy, $exe) }
        catch [IO.IOException] { if (!(Test-Path -LiteralPath $exe)) { throw } }
    } finally {
        [IO.File]::Delete($copy)
    }
}

if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $hash) {
    throw 'The installed executable does not match the tested build.'
}

$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'DinamicScreenSound.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $release
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'DinamicScreenSound desktop app'
$shortcut.Save()

[PSCustomObject]@{ Revision = $revision; Executable = $exe; Shortcut = $shortcutPath }
