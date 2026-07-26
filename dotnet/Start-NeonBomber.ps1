[CmdletBinding()]
param(
    [switch] $NoBrowser,
    [switch] $KeepHost,
    [switch] $Stop
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$appName = 'Neon Bomber'
$appPort = 54137
$appUrl = "http://127.0.0.1:$appPort/"
$projectPath = Join-Path $PSScriptRoot 'src\Bomber.Web\Bomber.Web.csproj'
$webAssemblyPath = Join-Path $PSScriptRoot 'src\Bomber.Web\bin\Release\net10.0\Bomber.Web.dll'
$stateDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'NeonBomber'
$standardLog = Join-Path $stateDirectory 'host.log'
$errorLog = Join-Path $stateDirectory 'host-error.log'

function Test-NeonBomberHost {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $appUrl -TimeoutSec 2
        return $response.StatusCode -eq 200 -and $response.Content.Contains('霓虹爆彈王')
    }
    catch {
        return $false
    }
}

function Get-NeonBomberListener {
    Get-NetTCPConnection -State Listen -LocalPort $appPort -ErrorAction SilentlyContinue |
        Select-Object -First 1
}

function Stop-NeonBomberHost {
    $listener = Get-NeonBomberListener
    if ($null -eq $listener) {
        return
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
    $ownsPort = $null -ne $process -and
        -not [string]::IsNullOrWhiteSpace($process.CommandLine) -and
        $process.CommandLine.Contains($webAssemblyPath, [StringComparison]::OrdinalIgnoreCase) -and
        $process.CommandLine.Contains('blazor-devserver.dll', [StringComparison]::OrdinalIgnoreCase) -and
        $process.CommandLine.Contains($appUrl, [StringComparison]::OrdinalIgnoreCase)
    if (-not $ownsPort) {
        throw "Port $appPort is owned by another application; it was not stopped."
    }

    Stop-Process -Id $listener.OwningProcess -Force
}

function Show-LaunchError([string] $message) {
    try {
        Add-Type -AssemblyName PresentationFramework
        [void][System.Windows.MessageBox]::Show(
            $message,
            "$appName could not start",
            [System.Windows.MessageBoxButton]::OK,
            [System.Windows.MessageBoxImage]::Error)
    }
    catch {
        Write-Error $message
    }
}

$startedHost = $false
$hostProcess = $null

try {
    if ($Stop) {
        Stop-NeonBomberHost
        return
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "The .NET game project was not found at $projectPath."
    }

    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    if (-not (Test-NeonBomberHost)) {
        if ($null -ne (Get-NeonBomberListener)) {
            throw "Port $appPort is already used by another application."
        }

        $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
        $arguments = @(
            'run',
            '--project', $projectPath,
            '--configuration', 'Release',
            '--no-restore',
            '--no-launch-profile',
            '--urls', $appUrl
        )
        $startParameters = @{
            FilePath               = $dotnetPath
            ArgumentList           = $arguments
            WorkingDirectory       = $PSScriptRoot
            WindowStyle            = 'Hidden'
            RedirectStandardOutput = $standardLog
            RedirectStandardError  = $errorLog
            PassThru               = $true
        }
        $hostProcess = Start-Process @startParameters
        $startedHost = $true

        $ready = $false
        for ($attempt = 0; $attempt -lt 180; $attempt++) {
            if ($hostProcess.HasExited) {
                break
            }
            if (Test-NeonBomberHost) {
                $ready = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }

        if (-not $ready) {
            $details = if (Test-Path -LiteralPath $errorLog) {
                (Get-Content -LiteralPath $errorLog -Tail 8) -join [Environment]::NewLine
            }
            else {
                'The local .NET host did not report an error.'
            }
            throw "The local game host did not become ready.$([Environment]::NewLine)$details"
        }
    }

    if ($NoBrowser) {
        Write-Output $appUrl
        return
    }

    $edgeCandidates = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) |
        Where-Object { $_ } |
        ForEach-Object { Join-Path $_ 'Microsoft\Edge\Application\msedge.exe' } |
        Where-Object { Test-Path -LiteralPath $_ }
    $edgePath = $edgeCandidates | Select-Object -First 1

    if ($edgePath) {
        $edgeProfile = Join-Path $stateDirectory 'EdgeProfile'
        New-Item -ItemType Directory -Path $edgeProfile -Force | Out-Null
        Start-Process -FilePath $edgePath -ArgumentList @(
            "--app=$appUrl",
            "--user-data-dir=$edgeProfile",
            '--no-first-run',
            '--disable-background-mode',
            # F11-equivalent fullscreen keeps the dedicated profile persistent;
            # Edge kiosk mode would use an InPrivate session and discard settings.
            '--start-fullscreen'
        ) | Out-Null

        if ($startedHost -and -not $KeepHost) {
            $sawAppProcess = $false
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $appProcesses = Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" |
                    Where-Object { $_.CommandLine -like "*$edgeProfile*" }
                if ($appProcesses) {
                    $sawAppProcess = $true
                    break
                }
                Start-Sleep -Milliseconds 500
            }

            while ($sawAppProcess) {
                Start-Sleep -Seconds 2
                $appProcesses = Get-CimInstance Win32_Process -Filter "Name='msedge.exe'" |
                    Where-Object { $_.CommandLine -like "*$edgeProfile*" }
                if (-not $appProcesses) {
                    break
                }
            }

            if ($sawAppProcess) {
                Stop-NeonBomberHost
            }
        }
    }
    else {
        Start-Process $appUrl | Out-Null
    }
}
catch {
    if ($startedHost) {
        try {
            if ($null -ne (Get-NeonBomberListener)) {
                Stop-NeonBomberHost
            }
            elseif ($null -ne $hostProcess -and -not $hostProcess.HasExited) {
                Stop-Process -Id $hostProcess.Id -Force
            }
        }
        catch {
            # Preserve the original launch error; cleanup is best-effort.
        }
    }
    Show-LaunchError $_.Exception.Message
    exit 1
}
