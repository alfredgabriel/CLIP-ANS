# Quiz Helper

Aplicación de escritorio Windows tipo **tray app** para estudiar tests de práctica.

- Detecta preguntas de test al copiarlas (Ctrl+C)
- Consulta Groq / OpenAI y obtiene la respuesta correcta
- Comunica el resultado cambiando el color del icono de la bandeja del sistema
- Sin popups, sin ventanas, sin fricción

## Stack

| Componente | Tecnología |
|---|---|
| Lenguaje | C# (.NET 8) |
| GUI | WPF |
| Tray | `System.Windows.Forms.NotifyIcon` |
| AI | Groq / OpenAI (OpenAI-compatible API) |
| API key storage | Windows Credential Manager (`PasswordVault`) |
| Config | `%APPDATA%\QuizHelper\config.json` |

## Requisitos

- Windows 10 / 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Instalación y ejecución

```powershell
git clone git@github.com:alfredgabriel/CLIP-ANS.git
cd CLIP-ANS

# Restaurar dependencias
dotnet restore src/QuizHelper/QuizHelper.csproj

# Ejecutar en modo desarrollo
dotnet run --project src/QuizHelper/QuizHelper.csproj
```

## Publicar como .exe único (sin consola)

```powershell
dotnet publish src/QuizHelper/QuizHelper.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish
```

El ejecutable aparece en `./publish/QuizHelper.exe`.

## Colores por defecto

| Letra | Color |
|---|---|
| A | 🔴 Rojo `#FF4444` |
| B | 🟢 Verde `#44FF66` |
| C | 🔵 Azul `#4488FF` |
| D | 🟡 Amarillo `#FFD700` |
| E | 🟣 Morado `#AA44FF` |

## Seguridad

La API key **nunca** se guarda en texto plano. Se almacena en el **Windows Credential Manager** ligado al usuario de Windows actual. Tampoco se registra en logs.

## Configuración

Al abrir la ventana principal (clic izquierdo en el icono de tray o doble clic):

1. Selecciona el proveedor (Groq / OpenAI)
2. Introduce la API key y pulsa **PROBAR CONEXIÓN**
3. Ajusta la longitud mínima de texto y el debounce si lo necesitas
4. Pulsa **GUARDAR CONFIGURACIÓN**

## Plan de commits

| Commit | Contenido |
|---|---|
| 1 | Scaffolding, tray persistente, ventana completa, estilos brutalistas |
| 2 | Watcher de portapapeles + integración con Groq/OpenAI |
| 3 | Persistencia de config + Windows Credential Manager |
| 4 | Leyenda de colores editable + blend para respuestas múltiples |
| 5 | Historial, manejo de errores, icono .ico propio, publicación |
