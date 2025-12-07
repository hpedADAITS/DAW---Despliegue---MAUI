param(
    [switch]$SkipEmulator,
    [switch]$SkipApi,
    
    [switch]$Fast
)

$ErrorActionPreference = "Continue"

function Write-Step { Write-Host ">>> $args" -ForegroundColor Cyan }
function Write-Success { Write-Host "[OK] $args" -ForegroundColor Green }
function Write-Err { Write-Host "[ERROR] $args" -ForegroundColor Red }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$emulatorName = "VSCode-MAUI-Android"
$mauiPath = Join-Path $scriptDir "MauiApp1\MauiApp1"
if ($Fast) { $configuration = "Debug" } else { $configuration = "Release" }


$dotnetCandidates = @(
    "C:\Program Files\dotnet",
    "C:\Program Files (x86)\dotnet"
)

$dotnetExe = "dotnet"
foreach ($candidate in $dotnetCandidates) {
    $candidateExe = Join-Path $candidate "dotnet.exe"
    if (Test-Path $candidateExe) {
        $dotnetExe = $candidateExe
        $env:PATH = "$candidate;$env:PATH"
        Write-Host "[INFO] Using dotnet from $candidateExe" -ForegroundColor Gray
        break
    }
}


$csprojPath = Join-Path $mauiPath "MauiApp1.csproj"
[xml]$csproj = Get-Content $csprojPath
$appPackage = $csproj.Project.PropertyGroup.ApplicationId

if (-not $appPackage) {
    Write-Err "Could not find ApplicationId in .csproj"
    exit 1
}

Write-Success "Package: $appPackage"


Write-Step "Building MAUI app ($configuration)..."
Push-Location $mauiPath

$msbuildArgs = @(
    "-f", "net9.0-android",
    "-c", $configuration,
    "-m",
    "/p:RunAnalyzers=false",
    "/p:AndroidUseSharedRuntime=$([bool]$Fast)",
    "/p:AndroidFastDeployment=$([bool]$Fast)"
)

$buildOutput = & $dotnetExe build @msbuildArgs 2>&1
$buildStatus = $?

if (-not $buildStatus) {
    Write-Err "Build failed"
    Write-Host ""
    Write-Host "=== BUILD OUTPUT ===" -ForegroundColor Red
    Write-Host $buildOutput
    Write-Host "==================" -ForegroundColor Red
    Pop-Location
    exit 1
}


$apkPath = Get-ChildItem -Path "bin\Release\net9.0-android\*\*.apk" -Recurse | Select-Object -First 1

if (-not $apkPath) {
    Write-Err "APK not found after build"
    Pop-Location
    exit 1
}

Write-Success "App built: $($apkPath.Name)"


if (-not $SkipEmulator) {
    Write-Step "Starting Android emulator..."
    $androidSdk = "C:\Program Files (x86)\Android\android-sdk"
    $emulatorExe = Join-Path $androidSdk "emulator\emulator.exe"
    
    if (Test-Path $emulatorExe) {
        $devices = & adb devices 2>$null
        $running = $devices | Select-String "emulator.*device"
        
        if (-not $running) {
            Start-Process -FilePath $emulatorExe -ArgumentList "-avd", $emulatorName, "-netdelay", "none", "-netspeed", "full" -WindowStyle Hidden
            Write-Success "Emulator starting (waiting 120 seconds for full boot)..."
            
            for ($i = 0; $i -lt 120; $i++) {
                Start-Sleep -Seconds 1
                $devices = & adb devices 2>$null
                $ready = $devices | Select-String "device$"
                if ($ready) {
                    Write-Success "Emulator ready!"
                    break
                }
            }
        } else {
            Write-Success "Emulator already running"
        }
    } else {
        Write-Err "Emulator not found. Run install.ps1 first."
        Pop-Location
        exit 1
    }
}


if (-not $SkipApi) {
    Write-Step "Starting API server..."
    
    
    Write-Host "  Cleaning up old processes..." -ForegroundColor Gray
    $existingProcessId = netstat -ano 2>$null | Select-String ":3000.*LISTENING" | ForEach-Object {
        $parts = $_ -split '\s+' | Where-Object { $_ }
        $procId = $parts[-1]
        if ($procId -and $procId -match '^\d+$') {
            $procId
        }
    } | Select-Object -First 1
    
    if ($existingProcessId) {
        try {
            Stop-Process -Id $existingProcessId -Force -ErrorAction SilentlyContinue
            Write-Success "Stopped existing process on port 3000"
            Start-Sleep -Seconds 1
        } catch {
            Write-Host "  Could not stop existing process (may not exist)" -ForegroundColor Gray
        }
    }
    
    $apiPath = Join-Path $scriptDir "api-server"
    
    $nodeModules = Join-Path $apiPath "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Step "Installing npm dependencies..."
        Push-Location $apiPath
        & npm install 2>&1 | Out-Null
        Pop-Location
        Write-Success "Dependencies installed"
    }
    
    
    $coversPath = Join-Path $apiPath "covers"
    if (-not (Test-Path $coversPath)) {
        Write-Step "Creating cover images directory..."
        New-Item -ItemType Directory -Path $coversPath -Force | Out-Null
        
        
        $colors = @(
            @{file='01.jpg'; color='1E3C72'},  
            @{file='02.jpg'; color='AA00FF'},  
            @{file='03.jpg'; color='FFAA00'},  
            @{file='04.jpg'; color='CC0000'},  
            @{file='05.jpg'; color='2A2A2A'},  
            @{file='06.jpg'; color='FF6600'},  
            @{file='07.jpg'; color='00CCFF'},  
            @{file='08.jpg'; color='005500'}   
        )
        
        foreach ($c in $colors) {
            $coverFile = Join-Path $coversPath $c.file
            
            
            $jpeg = @(
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
                0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
                0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
                0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
                0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
                0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
                0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
                0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
                0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00,
                0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0xFF, 0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03,
                0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D,
                0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
                0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
                0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72,
                0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
                0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45,
                0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
                0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75,
                0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
                0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
                0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
                0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9,
                0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
                0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4,
                0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
                0x00, 0x00, 0x3F, 0x00, 0xFB, 0xD5, 0xFF, 0xD9
            ) -as [byte[]]
            [System.IO.File]::WriteAllBytes($coverFile, $jpeg)
        }
        Write-Success "Created placeholder cover images"
    }
    
    
    $apiLogFile = Join-Path $env:TEMP "api-server-$([guid]::NewGuid().ToString().Substring(0,8)).log"
    $apiErrFile = Join-Path $env:TEMP "api-server-err-$([guid]::NewGuid().ToString().Substring(0,8)).log"
    $apiProcess = Start-Process -FilePath "node" -ArgumentList "server.js" -WorkingDirectory $apiPath -RedirectStandardOutput $apiLogFile -RedirectStandardError $apiErrFile -PassThru -WindowStyle Hidden
    
    Write-Success "API server started (PID: $($apiProcess.Id))"
    Write-Host "  Waiting for server to initialize..." -ForegroundColor Gray
    
    
    Write-Step "Checking API server health..."
    $apiReady = $false
    for ($i = 0; $i -lt 15; $i++) {
        try {
            
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:3000/api/status" -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $apiReady = $true
                Write-Success "API server is responding on localhost:3000"
                break
            }
        } catch {
            
            Write-Host "  Waiting... ($($i+1)/15)" -ForegroundColor Gray
            Start-Sleep -Seconds 1
        }
    }
    
    if (-not $apiReady) {
        Write-Host "  Note: If server is not responding, you can test manually:" -ForegroundColor Yellow
        Write-Host "    powershell -Command 'Invoke-WebRequest -Uri http://127.0.0.1:3000/api/status'" -ForegroundColor Yellow
    }
    
    Write-Host "  Emulator will reach API via: http://10.0.2.2:3000" -ForegroundColor Gray
}


Write-Step "Installing app to emulator..."

$installOutput = & adb install -r $apkPath.FullName 2>&1

if ($installOutput -match "Success" -or $installOutput -match "installed") {
    Write-Success "App installed successfully"
    
    Start-Sleep -Seconds 1
    Write-Step "Launching app..."
    
    
    $launchOutput = & adb shell monkey -p $appPackage 1 2>&1
    
    if ($launchOutput -match "Error") {
        Write-Err "Could not launch app"
        Write-Host "  Tip: Check launcher or go to Settings > Apps to find it" -ForegroundColor Yellow
    } else {
        Write-Success "App launched!"
    }
    
    Start-Sleep -Seconds 2
} else {
    Write-Err "Installation may have failed"
    Write-Host "  Output: $installOutput" -ForegroundColor Yellow
}

Pop-Location

Write-Host ""
Write-Host "SETUP COMPLETE" -ForegroundColor Green
Write-Host "  Emulator: $emulatorName"
Write-Host "  API: http://localhost:3000"
Write-Host "  App: $appPackage"
Write-Host ""
Write-Host "=== USEFUL ADB COMMANDS ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Check device status:" -ForegroundColor Gray
Write-Host "  adb devices" -ForegroundColor White
Write-Host ""
Write-Host "Install APK manually:" -ForegroundColor Gray
Write-Host "  adb install -r ""$($apkPath.FullName)""" -ForegroundColor White
Write-Host ""
Write-Host "Launch app:" -ForegroundColor Gray
Write-Host "  adb shell monkey -p $appPackage 1" -ForegroundColor White
Write-Host ""
Write-Host "View app logs:" -ForegroundColor Gray
Write-Host "  adb logcat | findstr $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Clear app cache:" -ForegroundColor Gray
Write-Host "  adb shell pm clear $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Uninstall app:" -ForegroundColor Gray
Write-Host "  adb uninstall $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Access emulator shell:" -ForegroundColor Gray
Write-Host "  adb shell" -ForegroundColor White
Write-Host ""
