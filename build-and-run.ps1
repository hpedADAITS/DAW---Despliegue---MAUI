param(
    [switch]$SkipEmulator,
    [switch]$SkipApi,
    
    [switch]$Fast,
    [switch]$UseDockerApi = $true,
    [string]$ApiImage = "hpedtor/hmauiserverapi:latest",
    [string]$ApiContainerName = "api-server"
)

$ErrorActionPreference = "Continue"

function Write-Step { Write-Host ">>> $args" -ForegroundColor Cyan }
function Write-Success { Write-Host "[OK] $args" -ForegroundColor Green }
function Write-Err { Write-Host "[ERROR] $args" -ForegroundColor Red }

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiPidFile = Join-Path $scriptDir "api-server\.api-server.pid"
$apiContainerExists = $false
$dockerAvailable = $false

function Stop-ExistingApiServer {
    param(
        [string]$PidFile,
        [string]$ApiPath
    )

    Write-Host "  Limpiando procesos API antiguos..." -ForegroundColor Gray
    $stopped = $false

    if (Test-Path $PidFile) {
        $pidText = Get-Content $PidFile -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($pidText -and $pidText -match '^\d+$') {
            $pidFromFile = [int]$pidText
            $proc = Get-Process -Id $pidFromFile -ErrorAction SilentlyContinue
            if ($proc) {
                try {
                    Stop-Process -Id $pidFromFile -Force -ErrorAction SilentlyContinue
                    Write-Success "Proceso API detenido desde archivo PID (PID: $pidFromFile)"
                    $stopped = $true
                } catch {
                    Write-Host "  No se pudo detener el PID $pidFromFile desde el archivo PID" -ForegroundColor Gray
                }
            }
        }
        Remove-Item $PidFile -ErrorAction SilentlyContinue
    }

    $serverPath = Join-Path $ApiPath "server.js"
    $nodeProcs = Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -and $_.CommandLine -match [regex]::Escape($serverPath)
    }

    foreach ($p in $nodeProcs) {
        try {
            Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Success "Proceso API existente detenido (PID: $($p.ProcessId))"
            $stopped = $true
        } catch {
            Write-Host "  No se pudo detener el proceso $($p.ProcessId)" -ForegroundColor Gray
        }
    }

    $existingProcessId = netstat -ano 2>$null | Select-String ":3000.*LISTENING" | ForEach-Object {
        $parts = $_ -split '\s+' | Where-Object { $_ }
        $procId = $parts[-1]
        if ($procId -and $procId -match '^\d+$') { $procId }
    } | Select-Object -First 1

    if ($existingProcessId) {
        try {
            Stop-Process -Id $existingProcessId -Force -ErrorAction SilentlyContinue
            Write-Success "Proceso detenido en el puerto 3000 (PID: $existingProcessId)"
            $stopped = $true
        } catch {
            Write-Host "  No se pudo detener el proceso existente en el puerto 3000" -ForegroundColor Gray
        }
    }

    if (-not $stopped) {
        Write-Host "  No se detectaron procesos API" -ForegroundColor Gray
    }
}

function Stop-DockerApi {
    param(
        [string]$ContainerName
    )

    if (-not $dockerAvailable) {
        return
    }

    $existingContainer = & docker ps -aq -f "name=$ContainerName" 2>$null
    if ($existingContainer) {
        Write-Host "  Deteniendo contenedor Docker existente ($ContainerName)..." -ForegroundColor Gray
        & docker rm -f $ContainerName | Out-Null
        Write-Success "Contenedor Docker detenido"
    }
}

function Start-DockerApi {
    param(
        [string]$Image,
        [string]$ContainerName
    )

    if (-not $dockerAvailable) {
        return $false
    }

    Write-Step "Iniciando API desde Docker ($Image)..."
    Stop-DockerApi -ContainerName $ContainerName

    $runResult = & docker run -d -p 3000:3000 --name $ContainerName $Image 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Err "No se pudo iniciar la API en Docker"
        Write-Host $runResult -ForegroundColor Yellow
        return $false
    }

    Write-Success "API en Docker ejecutándose (container: $ContainerName)"
    $script:apiContainerExists = $true
    return $true
}

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
        Write-Host "[INFO] Usando dotnet desde $candidateExe" -ForegroundColor Gray
        break
    }
}

# Detect Docker availability early
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $dockerAvailable = $true
} else {
    $dockerAvailable = $false
}


$csprojPath = Join-Path $mauiPath "MauiApp1.csproj"
[xml]$csproj = Get-Content $csprojPath
$appPackage = $csproj.Project.PropertyGroup.ApplicationId

if (-not $appPackage) {
    Write-Err "No se pudo encontrar ApplicationId en .csproj"
    exit 1
}

Write-Success "Paquete: $appPackage"


Write-Step "Compilando aplicacion MAUI ($configuration)..."
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
    Write-Err "Compilacion fallida"
    Write-Host ""
    Write-Host "=== SALIDA DE COMPILACION ===" -ForegroundColor Red
    Write-Host $buildOutput
    Write-Host "==================" -ForegroundColor Red
    Pop-Location
    exit 1
}


$apkPath = Get-ChildItem -Path "bin\Release\net9.0-android\*\*.apk" -Recurse | Select-Object -First 1

if (-not $apkPath) {
    Write-Err "No se encontro APK despues de compilar"
    Pop-Location
    exit 1
}

Write-Success "Aplicacion compilada: $($apkPath.Name)"


if (-not $SkipEmulator) {
    Write-Step "Iniciando emulador de Android..."
    $androidSdk = "C:\Program Files (x86)\Android\android-sdk"
    $emulatorExe = Join-Path $androidSdk "emulator\emulator.exe"
    
    if (Test-Path $emulatorExe) {
        $devices = & adb devices 2>$null
        $running = $devices | Select-String "emulator.*device"
        
        if (-not $running) {
            Start-Process -FilePath $emulatorExe -ArgumentList "-avd", $emulatorName, "-netdelay", "none", "-netspeed", "full" -WindowStyle Hidden
            Write-Success "Emulador iniciando (esperando 120 segundos para el arranque completo)..."
            
            for ($i = 0; $i -lt 120; $i++) {
                Start-Sleep -Seconds 1
                $devices = & adb devices 2>$null
                $ready = $devices | Select-String "device$"
                if ($ready) {
                    Write-Success "Emulador listo!"
                    break
                }
            }
        } else {
            Write-Success "Emulador ya en ejecucion"
        }
    } else {
        Write-Err "No se encontro el emulador. Ejecuta install.ps1 primero."
        Pop-Location
        exit 1
    }
}


if (-not $SkipApi) {
    Write-Step "Iniciando servidor API..."

    $apiStarted = $false

    if ($UseDockerApi) {
        if (-not $dockerAvailable) {
            Write-Host "Docker no está disponible; se usará Node.js local." -ForegroundColor Yellow
        } else {
            $apiStarted = Start-DockerApi -Image $ApiImage -ContainerName $ApiContainerName
        }
    }

    if (-not $apiStarted) {
        # Check if Node.js is installed
        $nodeExe = Get-Command node -ErrorAction SilentlyContinue
        if (-not $nodeExe) {
            Write-Err "Node.js no esta instalado o no esta en PATH"
            Write-Host "  Por favor instala Node.js desde https://nodejs.org/" -ForegroundColor Yellow
            Write-Host "  O agrega Node.js al PATH del sistema" -ForegroundColor Yellow
            exit 1
        }
        Write-Success "Node.js encontrado: $(node --version)"
        
        $apiPath = Join-Path $scriptDir "api-server"
        Stop-ExistingApiServer -PidFile $apiPidFile -ApiPath $apiPath
        
        Write-Step "Verificando que las dependencias de Node.js esten instaladas..."
        $nodeModules = Join-Path $apiPath "node_modules"
        if (-not (Test-Path $nodeModules)) {
            Write-Host "  node_modules no encontrado, ejecutando npm install..." -ForegroundColor Gray
            Push-Location $apiPath
            $npmOutput = & npm install 2>&1
            $npmSuccess = $?
            Pop-Location
            
            if (-not $npmSuccess) {
                Write-Err "npm install fallo"
                Write-Host $npmOutput
                exit 1
            }
            Write-Success "Dependencias instaladas"
        } else {
            Write-Host "  node_modules ya existe" -ForegroundColor Gray
        }
        
        
        $coversPath = Join-Path $apiPath "covers"
        if (-not (Test-Path $coversPath)) {
            Write-Step "Creando directorio de imagenes de portada..."
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
            Write-Success "Imagenes de portada de ejemplo creadas"
        }
        
        
        $apiLogFile = Join-Path $env:TEMP "api-server-$([guid]::NewGuid().ToString().Substring(0,8)).log"
        $apiErrFile = Join-Path $env:TEMP "api-server-err-$([guid]::NewGuid().ToString().Substring(0,8)).log"
        
        Write-Host "  Archivo de log: $apiLogFile" -ForegroundColor Gray
        Write-Host "  Archivo de errores: $apiErrFile" -ForegroundColor Gray
        
        $apiProcess = Start-Process -FilePath "node" -ArgumentList "server.js" -WorkingDirectory $apiPath -RedirectStandardOutput $apiLogFile -RedirectStandardError $apiErrFile -PassThru -WindowStyle Hidden
        Set-Content -Path $apiPidFile -Value $apiProcess.Id -Encoding ASCII -ErrorAction SilentlyContinue
        
        Write-Success "Servidor API iniciado (PID: $($apiProcess.Id))"
        Write-Host "  Esperando a que el servidor inicie..." -ForegroundColor Gray
        
        Start-Sleep -Seconds 2
        
        Write-Step "Comprobando estado del servidor API..."
        $apiReady = $false
        for ($i = 0; $i -lt 15; $i++) {
            try {
                $response = Invoke-WebRequest -Uri "http://127.0.0.1:3000/api/status" -TimeoutSec 2 -ErrorAction SilentlyContinue
                if ($response.StatusCode -eq 200) {
                    $apiReady = $true
                    Write-Success "Servidor API respondiendo en localhost:3000"
                    break
                }
            } catch {
                Write-Host "  Esperando... ($($i+1)/15)" -ForegroundColor Gray
                Start-Sleep -Seconds 1
            }
        }
        
        if (-not $apiReady) {
            Write-Host "  ADVERTENCIA: el servidor API podria no estar respondiendo!" -ForegroundColor Yellow
            Write-Host "  Archivo de log: $apiLogFile" -ForegroundColor Yellow
            Write-Host "  Archivo de errores: $apiErrFile" -ForegroundColor Yellow
            Write-Host "  Revisa los logs anteriores para errores y asegura que Node.js este instalado." -ForegroundColor Yellow
            Write-Host "  Prueba manualmente con: Invoke-WebRequest -Uri http://127.0.0.1:3000/api/status" -ForegroundColor Yellow
        }
    }

    Write-Host "  El emulador accedera a la API via: http://10.0.2.2:3000" -ForegroundColor Gray
}


Write-Step "Instalando la aplicacion en el emulador..."

$installOutput = & adb install -r $apkPath.FullName 2>&1

if ($installOutput -match "Success" -or $installOutput -match "installed") {
    Write-Success "Aplicacion instalada correctamente"
    
    Start-Sleep -Seconds 1
    Write-Step "Iniciando aplicacion..."
    
    
    $launchOutput = & adb shell monkey -p $appPackage 1 2>&1
    
    if ($launchOutput -match "Error") {
        Write-Err "No se pudo iniciar la aplicacion"
        Write-Host "  Sugerencia: revisa el lanzador o ve a Ajustes > Apps para encontrarla" -ForegroundColor Yellow
    } else {
        Write-Success "Aplicacion iniciada!"
    }
    
    Start-Sleep -Seconds 2
} else {
    Write-Err "La instalacion pudo haber fallado"
    Write-Host "  Salida: $installOutput" -ForegroundColor Yellow
}

Pop-Location

Write-Host ""
Write-Host "CONFIGURACION COMPLETA" -ForegroundColor Green
Write-Host "  Emulador: $emulatorName"
Write-Host "  API: http://localhost:3000"
Write-Host "  Aplicacion: $appPackage"
Write-Host ""
Write-Host "=== COMANDOS ADB UTILES ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ver estado del dispositivo:" -ForegroundColor Gray
Write-Host "  adb devices" -ForegroundColor White
Write-Host ""
Write-Host "Instalar APK manualmente:" -ForegroundColor Gray
Write-Host "  adb install -r ""$($apkPath.FullName)""" -ForegroundColor White
Write-Host ""
Write-Host "Iniciar aplicacion:" -ForegroundColor Gray
Write-Host "  adb shell monkey -p $appPackage 1" -ForegroundColor White
Write-Host ""
Write-Host "Ver logs de la aplicacion:" -ForegroundColor Gray
Write-Host "  adb logcat | findstr $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Limpiar cache de la aplicacion:" -ForegroundColor Gray
Write-Host "  adb shell pm clear $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Desinstalar app:" -ForegroundColor Gray
Write-Host "  adb uninstall $appPackage" -ForegroundColor White
Write-Host ""
Write-Host "Acceder a la shell del emulador:" -ForegroundColor Gray
Write-Host "  adb shell" -ForegroundColor White
Write-Host ""
