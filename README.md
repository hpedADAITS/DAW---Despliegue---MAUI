# Reproductor de Audio – MAUI y Express

Aplicación multiplataforma de reproductor de audio construida con **.NET 9 MAUI** y **Express.js**, con controles modernos de reproducción, gestión de listas de reproducción y API REST integrada.

## Características

**Controles de Reproducción**
- Reproducir, pausar, detener, siguiente y anterior
- Deslizador de progreso con indicador de tiempo
- Control de volumen

**Gestión de Listas de Reproducción**
- Carga dinámica de canciones desde API REST
- Interfaz para gestionar pistas
- Sincronización automática con el servidor

**Interfaz de Usuario**
- Tema oscuro con diseño moderno e intuitivo
- Interfaz de múltiples pestañas para navegación
- Visualización de género y metadatos
- Diseño adaptable para todas las plataformas

**Compatibilidad Multiplataforma**
- Android (API 21+)
- iOS (15.0+)
- macOS (Catalyst, 15.0+)
- Windows (10.0.17763.0+)

**Funciones Avanzadas**
- Modos de repetición: desactivado, todos, uno
- Información de pistas por género
- Integración en tiempo real con la API
- Soporte para múltiples formatos de audio (MP3, WAV, M4A)

## Inicio Rápido

### Requisitos Previos

- **Windows/macOS/Linux** con PowerShell o bash
- **.NET 9 SDK** ([descargar](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Visual Studio 2022** (opcional para desarrollo con IDE) o **VS Code** con extensión de C#
- **Node.js 18+** (para el servidor API)
- **Android SDK/NDK** (para compilaciones Android)
- **Xcode 15+** (para compilaciones iOS en macOS)

### Configuración y Ejecución

**Configuración automática completa** (emulador, API, instalación de la app):
```powershell
pwsh -ExecutionPolicy Bypass -File .\build-and-run.ps1 -AcceptLicenses -StartEmulator
```

**Si el script falla, inicia la API manualmente (pasos en español):**
```bash
cd api-server          # desde la raíz del repo
npm install            # solo la primera vez
npm run dev            # levanta el servidor en http://localhost:3000
```
Luego abre otro terminal para compilar la app MAUI.

**O de forma manual:**

1. **Inicia el servidor API** (desde la raíz del proyecto):
   ```bash
   cd api-server
   npm install
   npm run dev
   ```

2. **Compila y ejecuta la app MAUI**:
   ```bash
   cd MauiApp1/MauiApp1
   dotnet build -f net9.0-android -c Release
   ```

3. Reinicia VS Code para detectar el emulador de Android y despliega desde el IDE.

## Estructura del Proyecto

```
DAW---Despliegue---MAUI/
├── MauiApp1/                      # Aplicación .NET 9 MAUI
│   └── MauiApp1/
│       ├── MainPage.xaml(.cs)     # UI principal y lógica de reproducción
│       ├── App.xaml(.cs)          # Raíz y configuración de la aplicación
│       ├── AppShell.xaml(.cs)     # Navegación de la aplicación
│       ├── AudioPlayer.cs         # Lógica del reproductor de audio
│       ├── MauiProgram.cs         # Inicialización de MAUI
│       ├── Views/                 # Vistas de la interfaz de usuario
│       ├── Platforms/             # Código específico por plataforma
│       ├── Resources/             # Iconos, fuentes, recursos
│       └── MauiApp1.csproj        # Archivo de proyecto
├── api-server/                    # Backend Express.js
│   ├── server.js                  # Servidor principal
│   ├── package.json               # Dependencias de Node.js
│   ├── songs/                     # Directorio de archivos de audio
│   ├── covers/                    # Imágenes de carátulas de canciones
│   └── generate-*.js              # Utilidades para generar datos de prueba
├── build-and-run.ps1              # Script de configuración del emulador Android
├── install.ps1                    # Instalador de dependencias
└── AGENTS.md                       # Documentación de configuración
```

## Endpoints de la API

El servidor Express se ejecuta en `http://localhost:3000` y expone los siguientes endpoints:

| Endpoint | Método | Respuesta |
|----------|--------|-----------|
| `/api/status` | GET | `{ app, api, serverTime }` |
| `/api/greeting` | GET | `{ title, subtitle, serverTime }` |
| `/api/songs` | GET | Array de canciones con metadatos e URLs dinámicas |
| `/api/songs/:id` | GET | Canción individual por ID |
| `/api/counter` | GET/POST | Estado del contador (ejemplo de mutación) |
| `/api/weather` | GET | Datos del clima con temperaturas en Celsius y Fahrenheit |
| `/api/radios/search` | GET | Búsqueda de estaciones de radio por término |
| `/api/radios/topvoted` | GET | Estaciones de radio más votadas |
| `/api/radios/random` | GET | Estaciones de radio aleatorias |
| `/api/radios/variety` | GET | Mezcla variada de estaciones de radio |

### Ejemplo: Obtener todas las canciones

```bash
curl http://localhost:3000/api/songs
```

Respuesta:
```json
[
  {
    "id": 1,
    "title": "Título de la Canción",
    "artist": "Nombre del Artista",
    "duration": "4:00",
    "durationSeconds": 240,
    "genre": "Pop",
    "file": "01.mp3",
    "bitrate": 128,
    "format": "mp3",
    "url": "http://localhost:3000/songs/01.mp3",
    "coverUrl": "http://localhost:3000/covers/01.jpg"
  },
  ...
]
```

### Ejemplo: Buscar estaciones de radio

```bash
curl "http://localhost:3000/api/radios/search?searchterm=jazz&limit=10"
```

## Conectar la App con la API

La app MAUI carga las canciones automáticamente al iniciar:

- **Emulador de Android**: el enrutamiento automático de `localhost` a `10.0.2.2` (máquina host)
- **Dispositivos físicos/simuladores**: actualiza `ApiUrl` en `MainPage.xaml.cs`:
  ```csharp
  private const string ApiUrl = "http://192.168.x.x:3000/api/songs";
  ```

## Arquitectura

### .NET MAUI (Cliente)
- **Framework**: .NET 9
- **UI**: XAML con C# code-behind
- **Navegación**: Shell navigation con varias vistas
- **Red**: HttpClient para llamadas a la API
- **Hilos**: `MainThread.BeginInvokeOnMainThread()` para actualizar la UI
- **Plataformas objetivo**: Android (API 21+), iOS (15.0+), macOS (15.0+), Windows (10.0.17763.0+)

### Express.js (Servidor)
- **Framework**: Express 5.2.1
- **CORS**: Configurado para `localhost`, `127.0.0.1` y `10.0.2.2` (emulador Android)
- **Escaneo dinámico**: Detecta automáticamente archivos de audio en el directorio `songs/`
- **Metadatos**: Extrae información de archivos MP3, WAV y M4A
- **Caché**: Almacena metadatos en caché durante 1 hora para optimizar rendimiento
- **Radio**: Integración con Radio Browser API para estaciones de radio en línea

## Compilación para Diferentes Plataformas

### Android
```bash
cd MauiApp1/MauiApp1
dotnet build -f net9.0-android -c Release
```

### iOS (solo en macOS)
```bash
cd MauiApp1/MauiApp1
dotnet build -f net9.0-ios -c Release
```

### macOS Catalyst
```bash
cd MauiApp1/MauiApp1
dotnet build -f net9.0-maccatalyst -c Release
```

### Windows
```bash
cd MauiApp1/MauiApp1
dotnet build -f net9.0-windows -c Release
```

## Flujo de Desarrollo

### Ejecutar la API en Modo Desarrollo
```bash
cd api-server
npm run dev  # Reinicio automático al cambiar archivos
```

### Ejecutar la App MAUI
- **Visual Studio 2022**: abre `MauiApp1.sln`, elige plataforma de destino y presiona F5
- **VS Code**: usa la extensión de .NET para compilar y desplegar
- **CLI**: usa `dotnet build` y despliega desde el IDE

### Estilo de Código

**C#:**
- PascalCase para clases y métodos; camelCase para campos privados
- Tipos de referencia anulables habilitados
- Usings implícitos habilitados
- Actualización de UI: `MainThread.BeginInvokeOnMainThread()`
- Manejo de errores: Try-catch con `DisplayAlert()` para errores

**JavaScript:**
- camelCase para variables y funciones
- Características ES6+ (funciones flecha, destructuring)
- Endpoints sin estado; estado en objetos planos
- Uso de `.map()` y spread operator para transformaciones

## Dependencias

### App MAUI
- .NET 9.0
- Microsoft.Maui.Controls (9.0.x)
- Microsoft.Extensions.Logging.Debug (9.0.8)

### Servidor API
- express: ^5.2.1
- cors: ^2.8.5
- music-metadata: ^8.1.3
- radio-browser: ^2.2.3
- nodemon: ^3.1.11 (solo en desarrollo)

## Solución de Problemas

### Emulador de Android No Detectado
```powershell
pwsh -ExecutionPolicy Bypass -File .\build-and-run.ps1 -Force -StartEmulator
# Reinicia VS Code después de que termine el script
```

### Errores de Conexión con la API
- Asegura que el servidor API esté ejecutándose: `npm run dev` desde `api-server/`
- Verifica la configuración del firewall en el puerto 3000
- Comprueba que el emulador de Android use `10.0.2.2` para localhost
- Confirma que haya archivos de audio en el directorio `api-server/songs/`

### Errores de Compilación
- Limpia la caché de NuGet: `dotnet nuget locals all --clear`
- Elimina los directorios `bin/` y `obj/`
- Restaura los paquetes: `dotnet restore`

### Sin Canciones en la Aplicación
- Verifica que existan archivos de audio (MP3, WAV, M4A) en `api-server/songs/`
- Usa los scripts de utilidad para generar datos de prueba:
  ```bash
  cd api-server
  npm install
  node generate-test-audio.js
  node generate-covers.js
  ```

## Mejoras Futuras

- Reproducción real de archivos de audio con MediaElement
- Mezcla y orden avanzado de pistas
- Funcionalidad de búsqueda y filtros avanzados
- Historial de reproducidas recientemente
- Sistema de favoritos y marcadores
- Reproducción en segundo plano y controles en pantalla de bloqueo
- Sincronización en la nube entre dispositivos

## Licencia

Licencia ISC – consulta el archivo LICENSE para más detalles

## Contribuciones

Las contribuciones son bienvenidas. Por favor:
1. Haz un fork del repositorio
2. Crea una rama de característica (`git checkout -b feature/tu-feature`)
3. Realiza commits con mensajes claros
4. Empuja la rama y abre un Pull Request

## Contacto

Para problemas, preguntas o sugerencias, abre un issue en GitHub.

---

**Construido con .NET 9 MAUI y Express.js**
