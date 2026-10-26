[CmdletBinding()]
param(
    [switch]$NoPause,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = Join-Path $ProjectDir 'HamTool.csproj'
$LogFile = Join-Path $ProjectDir 'build-log.txt'

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}
function Finish([int]$Code) {
    if (-not $NoPause -and $Host.Name -eq 'ConsoleHost') {
        Write-Host ''
        Read-Host 'Press Enter to close'
    }
    exit $Code
}

Set-Location $ProjectDir
if (-not (Test-Path $ProjectFile)) {
    Write-Host "ERROR: Could not find $ProjectFile" -ForegroundColor Red
    Finish 1
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host 'ERROR: .NET SDK not found. Install .NET 10 SDK for Windows:' -ForegroundColor Red
    Write-Host 'https://dotnet.microsoft.com/download/dotnet/10.0'
    Finish 1
}

$version = (& dotnet --version 2>&1 | Out-String).Trim()
Write-Host "HamTool build script | .NET SDK: $version"
Write-Host "Project: $ProjectDir"
Write-Host "Log: $LogFile"

try {
    if ($Clean) {
        Write-Step 'Cleaning previous build outputs'
        if (Test-Path (Join-Path $ProjectDir 'bin')) { Remove-Item (Join-Path $ProjectDir 'bin') -Recurse -Force }
        if (Test-Path (Join-Path $ProjectDir 'obj')) { Remove-Item (Join-Path $ProjectDir 'obj') -Recurse -Force }
    }

    "HamTool build started: $(Get-Date -Format o)`r`nSDK: $version`r`n" | Set-Content -Path $LogFile -Encoding UTF8

    Write-Step 'Restoring NuGet packages'
    & dotnet restore $ProjectFile 2>&1 | Tee-Object -FilePath $LogFile -Append
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Restore failed. See $LogFile" -ForegroundColor Red
        Finish $LASTEXITCODE
    }

    Write-Step 'Building Release configuration'
    & dotnet build $ProjectFile --no-restore -c Release 2>&1 | Tee-Object -FilePath $LogFile -Append
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED. Copy the error lines from the console or send build-log.txt." -ForegroundColor Red
        Write-Host "Full log: $LogFile"
        Finish $LASTEXITCODE
    }

    Write-Host "`nBUILD SUCCEEDED" -ForegroundColor Green
    Write-Host 'Executable output: bin\Release\net10.0-windows\HamTool.exe'
    Write-Host 'Note: this is a WPF Windows app; build it on Windows with the .NET 10 SDK.'
    Finish 0
}
catch {
    $_ | Out-String | Add-Content -Path $LogFile -Encoding UTF8
    Write-Host "Unexpected build-script error: $_" -ForegroundColor Red
    Write-Host "See $LogFile"
    Finish 1
}
