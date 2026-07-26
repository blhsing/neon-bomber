[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$iconBuilder = Join-Path $PSScriptRoot 'tools\Build-AppIcon.ps1'
$launcherProject = Join-Path $PSScriptRoot 'src\Bomber.Launcher\Bomber.Launcher.csproj'
$webProject = Join-Path $PSScriptRoot 'src\Bomber.Web\Bomber.Web.csproj'
$launcherScript = Join-Path $PSScriptRoot 'Start-NeonBomber.ps1'
$installDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Neon Bomber'
$launcherExecutable = Join-Path $installDirectory 'NeonBomber.exe'
$startMenuDirectory = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenuDirectory 'Neon Bomber (.NET).lnk'

& $iconBuilder | Out-Null
$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnetPath build $webProject `
    --configuration Release `
    --nologo
if ($LASTEXITCODE -ne 0) {
    throw "The web game build failed with exit code $LASTEXITCODE."
}

& $dotnetPath publish $launcherProject `
    --configuration Release `
    --output $installDirectory `
    --nologo
if ($LASTEXITCODE -ne 0) {
    throw "The native launcher publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $launcherExecutable -PathType Leaf)) {
    throw "The launcher was not published to $launcherExecutable."
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $launcherExecutable
$shortcut.Arguments = '"' + $launcherScript + '"'
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.IconLocation = "$launcherExecutable,0"
$shortcut.Description = 'Launch the .NET edition of Neon Bomber'
$shortcut.Save()

$iconRefresh = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
if (Test-Path -LiteralPath $iconRefresh) {
    Start-Process -FilePath $iconRefresh -ArgumentList '-show' -Wait -WindowStyle Hidden
}

Write-Output $shortcutPath
