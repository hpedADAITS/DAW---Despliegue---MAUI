
[CmdletBinding()]
param(
    [string]$AvdName = "VSCode-MAUI-Android",
    [string]$AndroidPlatform = "android-34",
    [string]$SystemImage = "google_apis;x86_64",
    [switch]$AcceptLicenses,
    [switch]$StartEmulator,
    [switch]$Force
)

function ConvertTo-ArgumentString {
    param([string[]]$Arguments)
    $builder = New-Object System.Text.StringBuilder
    foreach ($arg in $Arguments) {
        if ($null -eq $arg) { continue }
        if ($builder.Length -gt 0) { [void]$builder.Append(' ') }
        $needsQuotes = $arg -match '\s'
        $escaped = $arg.Replace('"', '\"')
        if ($needsQuotes) {
            [void]$builder.Append('"').Append($escaped).Append('"')
        } else {
            [void]$builder.Append($escaped)
        }
    }
    return $builder.ToString()
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [string[]]$Arguments = @(),
        [string[]]$InputLines = @()
    )

    if (-not (Test-Path -Path $FilePath)) {
        throw "Required tool '$FilePath' was not found."
    }

    $argumentString = ConvertTo-ArgumentString -Arguments $Arguments
    Write-Host ">> $FilePath $argumentString"

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.Arguments = $argumentString
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = ($InputLines.Count -gt 0)
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardError = $false

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $null = $process.Start()

    if ($psi.RedirectStandardInput) {
        foreach ($line in $InputLines) {
            $process.StandardInput.WriteLine($line)
        }
        $process.StandardInput.Close()
    }

    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Command failed with exit code $($process.ExitCode): $FilePath $argumentString"
    }
}

function Get-AndroidSdkRoot {
    $candidatePaths = New-Object System.Collections.Generic.List[string]
    foreach ($val in @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)) {
        if (-not [string]::IsNullOrWhiteSpace($val)) {
            $candidatePaths.Add($val)
        }
    }
    $localSdk = Join-Path -Path ([Environment]::GetFolderPath('LocalApplicationData')) -ChildPath "Android\Sdk"
    $candidatePaths.Add($localSdk)
    $candidatePaths.Add("C:\Program Files (x86)\Android\android-sdk")
    $candidatePaths.Add("C:\Program Files\Android\android-sdk")

    foreach ($path in $candidatePaths) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }

    throw "Android SDK not found. Install the Android SDK (via Visual Studio or Android Studio) or set ANDROID_SDK_ROOT."
}

function Resolve-CmdlineToolsPath {
    param([string]$SdkRoot)

    if ([string]::IsNullOrWhiteSpace($SdkRoot)) {
        throw "SDK root not provided."
    }

    $cmdline = Join-Path $SdkRoot "cmdline-tools"
    $latest = Join-Path $cmdline "latest"
    if (Test-Path $latest) {
        return (Resolve-Path $latest).Path
    }

    $installed = Get-ChildItem -Path $cmdline -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if ($null -eq $installed) {
        throw "cmdline-tools not found. Install them with 'sdkmanager ""cmdline-tools;latest""'."
    }

    return $installed.FullName
}

function Set-UserEnvironmentVariable {
    param([string]$Name, [string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return }
    [Environment]::SetEnvironmentVariable($Name, $Value, 'User')
    Write-Host "Set user environment variable $Name = $Value"
}

function Add-PathSegment {
    param([string]$Segment)
    if ([string]::IsNullOrWhiteSpace($Segment)) { return }
    if (-not (Test-Path $Segment)) { return }
    $current = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (-not $current) { $current = "" }
    $parts = $current.Split(';') | Where-Object { $_ }
    if ($parts -contains $Segment) { return }
    $newPath = ($parts + $Segment) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
    Write-Host "Added $Segment to the user PATH."
}

$sdkRoot = Get-AndroidSdkRoot
Write-Host "Using Android SDK at $sdkRoot"

$cmdlineTools = Resolve-CmdlineToolsPath -SdkRoot $sdkRoot
$cmdlineBin = Join-Path $cmdlineTools "bin"
$sdkManager = Join-Path $cmdlineBin "sdkmanager.bat"
$avdManager = Join-Path $cmdlineBin "avdmanager.bat"
$emulatorExe = Join-Path $sdkRoot "emulator\emulator.exe"
$adbExe = Join-Path $sdkRoot "platform-tools\adb.exe"

foreach ($tool in @(@{Path=$sdkManager; Name="sdkmanager"},
                   @{Path=$avdManager; Name="avdmanager"})) {
    if (-not (Test-Path $tool.Path)) {
        throw "Required tool '$($tool.Name)' was not found at $($tool.Path). Install Android command-line tools."
    }
}

Set-UserEnvironmentVariable -Name "ANDROID_SDK_ROOT" -Value $sdkRoot
Set-UserEnvironmentVariable -Name "ANDROID_HOME" -Value $sdkRoot
$platformTools = Join-Path $sdkRoot "platform-tools"
$emulatorDir = Join-Path $sdkRoot "emulator"
Add-PathSegment -Segment $platformTools
Add-PathSegment -Segment $emulatorDir

$packageId = "system-images;$AndroidPlatform;$SystemImage"
$packages = @(
    "platform-tools",
    "emulator",
    "platforms;$AndroidPlatform",
    $packageId
)

$licenseResponses = if ($AcceptLicenses) { 1..20 | ForEach-Object { "y" } } else { @() }
$installArguments = @("--install") + $packages
Invoke-ExternalCommand -FilePath $sdkManager -Arguments $installArguments -InputLines $licenseResponses
$platformTools = Join-Path $sdkRoot "platform-tools"
$emulatorDir = Join-Path $sdkRoot "emulator"
Add-PathSegment -Segment $platformTools
Add-PathSegment -Segment $emulatorDir

$avdDir = Join-Path -Path (Join-Path $env:USERPROFILE ".android\avd") -ChildPath "$AvdName.avd"
$needCreate = $Force -or -not (Test-Path $avdDir)
if ($needCreate) {
    $avdHome = Split-Path $avdDir
    if (-not (Test-Path $avdDir)) {
        Write-Host "Creating AVD '$AvdName'..."
    } else {
        Write-Host "Recreating AVD '$AvdName'..."
        $iniPath = Join-Path $avdHome "$AvdName.ini"
        if (Test-Path $avdDir) { Remove-Item -Recurse -Force -Path $avdDir }
        if (Test-Path $iniPath) { Remove-Item -Force -Path $iniPath }
    }
    $createArgs = @(
        "create", "avd",
        "-n", $AvdName,
        "-k", $packageId,
        "-d", "pixel_6"
    )
    if ($Force) { $createArgs += "-f" }
    Invoke-ExternalCommand -FilePath $avdManager -Arguments $createArgs -InputLines @("no")
} else {
    Write-Host "AVD '$AvdName' already exists. Use -Force to recreate it."
}

Write-Host ""
Write-Host "Android emulator '$AvdName' is ready."
Write-Host "Make sure to restart VS Code or your terminal so the updated environment variables take effect."

if (-not (Test-Path $adbExe)) {
    Write-Warning "adb executable could not be located at $adbExe."
}

if ($StartEmulator) {
    Write-Host ""
    Write-Host "Launching emulator..."
    Invoke-ExternalCommand -FilePath $emulatorExe -Arguments @("-avd", $AvdName, "-netdelay", "none", "-netspeed", "full")
    Write-Host "Use 'adb devices' to ensure VS Code can detect the running emulator."
} else {
    Write-Host ""
    Write-Host "Start the emulator when needed with:"
    Write-Host "  `"$emulatorExe`" -avd $AvdName"
}
