# 🤖 CLIP-ANS
### Copiloto de IA para Exámenes y Tests — Vive en tu Portapapeles
*AI-powered exam copilot that lives in your clipboard and answers questions in real time.*

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Built with WPF](https://img.shields.io/badge/Built%20with-WPF%20%2F%20.NET%209-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![AI: Groq / OpenAI](https://img.shields.io/badge/AI-Groq%20%7C%20OpenAI-00B4D8)](https://console.groq.com/)
[![UI: Brutalist](https://img.shields.io/badge/UI-Brutalist%20Monochrome-black)](https://github.com/alfredgabriel/CLIP-ANS)

---

## 🇪🇸 Español

### 1. Visión y Propósito

**CLIP-ANS** es una aplicación de escritorio para Windows que detecta automáticamente cuando copias una pregunta de examen (texto o captura de pantalla) y te devuelve la respuesta en menos de un segundo, visible como el color del indicador de la bandeja del sistema.

Diseñada con una interfaz **brutalist** en blanco y negro, permanece oculta en la barra del sistema y solo actúa cuando la necesitas. Cero fricción, máxima velocidad.

---

### 2. Características

- **Detección de portapapeles en tiempo real** — Monitoriza el portapapeles continuamente; en cuanto detecta una pregunta (texto ≥ 20 chars) la envía a la IA automáticamente.
- **OCR integrado** — Detecta capturas de pantalla (screenshots) copiados al portapapeles y extrae el texto mediante reconocimiento óptico antes de enviarlo.
- **Indicador visual de color** — Cada opción de respuesta (A, B, C, D…) tiene un color configurable. El icono de la bandeja del sistema cambia al color de la respuesta correcta al instante.
- **Preguntas de desarrollo** — Si la pregunta no tiene opciones, la IA responde con texto libre. El resultado se puede copiar con un clic.
- **Auto-failover de modelo** — Si el modelo principal se agota o está no disponible, cambia automáticamente al siguiente candidato sin interrupciones.
- **Historial de sesión** — Muestra las últimas 10 preguntas respondidas con hora, respuesta y vista previa de la pregunta.
- **Colores personalizables** — Cambia el color de cada opción (A-Z) con un selector de color nativo de Windows.
- **Soporte multi-proveedor** — Compatible con **Groq** (gratuito) y **OpenAI** (GPT-4o).
- **API Key segura** — La clave se almacena en **Windows Credential Manager**, nunca en texto plano.

---

### 3. Requisitos

- Windows 10 / 11 (64-bit)
- .NET 9 Runtime (incluido en el instalador)
- Conexión a internet
- **API Key de Groq** (gratuita) **o** API Key de OpenAI

---

### 4. Instalación

#### Opción A — Instalador (recomendado)
Descarga el instalador `CLIP-ANS-Setup.exe` desde [Releases](https://github.com/alfredgabriel/CLIP-ANS/releases) y ejecútalo.

#### Opción B — Compilar desde fuente
```bash
git clone https://github.com/alfredgabriel/CLIP-ANS.git
cd CLIP-ANS
dotnet publish src\CLIP-ANS\CLIP-ANS.csproj -c Release -o .\publish\
.\publish\CLIP-ANS.exe
```

---

### 5. Configuración de la API Key

> ⚠️ **Sin una API Key válida la aplicación no puede responder preguntas.** El proceso es gratuito y tarda menos de 2 minutos.

#### 🔑 Obtener una API Key de Groq (GRATIS)

1. Ve a **[console.groq.com](https://console.groq.com/)** e inicia sesión o crea una cuenta gratuita.
2. En el panel izquierdo, haz clic en **"API Keys"**.
3. Pulsa **"+ Create API Key"**, escribe un nombre (ej: `CLIP-ANS`) y haz clic en **Create**.
4. **IMPORTANTE:** Copia la clave completa en ese momento (empieza por `gsk_...`). Solo se muestra una vez.
5. Guarda la clave en un lugar seguro antes de cerrar la ventana.

#### ⚙️ Introducir la API Key en CLIP-ANS

1. Abre CLIP-ANS (icono en la bandeja del sistema → doble clic o clic derecho → Abrir).
2. En la sección **"Proveedor IA"**, selecciona `GROQ` como proveedor.
3. Pega tu clave en el campo **"API KEY"**.
4. Haz clic en **"Probar Conexión"** para verificar que funciona correctamente.
5. Si la prueba es exitosa, la configuración se guarda automáticamente. ✅

#### 🔑 Usar OpenAI en su lugar (opcional)

1. Ve a **[platform.openai.com/api-keys](https://platform.openai.com/api-keys)** y crea una clave.
2. En CLIP-ANS, cambia el proveedor a `OPENAI` e introduce tu clave.
3. Selecciona el modelo (`gpt-4o` o `gpt-4o-mini`) y pulsa **"Probar Conexión"**.

---

### 6. Modo de Uso

#### Responder una pregunta de test
1. **Copia** la pregunta con sus opciones (`Ctrl+C`) desde cualquier lugar.
2. El icono de la bandeja cambia de color en menos de 1 segundo → ese color es la respuesta correcta.
3. Consulta la sección **"Estado en tiempo real"** en la ventana principal si necesitas más detalle.

#### Responder con captura de pantalla (OCR)
1. Haz una captura de la pregunta (`Win+Shift+S` o `PrintScreen`).
2. CLIP-ANS detecta la imagen en el portapapeles, extrae el texto con OCR y responde automáticamente.

#### Pregunta de desarrollo (respuesta abierta)
1. Si la pregunta no tiene opciones A/B/C/D, la IA responde con texto completo.
2. Aparece en el panel **"Estado en tiempo real"** con un botón **Copiar**.

---

### 7. Colores de las Opciones

Cada letra (A, B, C, D, E…) tiene un color asignado visible en la sección **"Colores de opciones"**.

- Haz clic en cualquier barra de color para cambiarlo con el selector nativo de Windows.
- Pulsa **Restaurar** para volver a los colores por defecto.
- Puedes añadir más opciones (F, G, H…) con el botón **+ Añadir opción**.

---

### 8. Comportamiento y Ajustes

| Ajuste | Descripción | Por defecto |
|---|---|---|
| **Detectar texto copiado** | Analiza cualquier texto copiado al portapapeles | ✅ Activado |
| **Detectar capturas (OCR)** | Analiza imágenes copiadas mediante OCR | ✅ Activado |
| **Notificaciones de Windows** | Muestra notificaciones del sistema al responder | ❌ Desactivado |
| **Longitud mínima de texto** | Ignora textos más cortos que este umbral (anti-spam) | 20 chars |
| **Debounce (ms)** | Espera antes de enviar a la IA (evita llamadas redundantes) | 400 ms |

---

### 🛠️ Desarrollo y Compilación

#### Requisitos
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Windows 10/11

#### Ejecutar en desarrollo
```bash
dotnet run --project src\CLIP-ANS\CLIP-ANS.csproj
```

#### Publicar ejecutable de producción
```bash
dotnet publish src\CLIP-ANS\CLIP-ANS.csproj -c Release -o .\publish\
```

#### Generar instalador (Inno Setup)
```powershell
.\installer\build.ps1
```

---

## 🇬🇧 English

### 1. Overview & Purpose

**CLIP-ANS** is a Windows desktop application that silently monitors your clipboard and answers exam questions in real time using AI, displaying the answer as a color on the system tray icon.

Built with a **brutalist** black-and-white aesthetic, it stays hidden until you need it. Zero friction, maximum speed.

---

### 2. Features

- **Real-time clipboard detection** — Monitors clipboard continuously; sends any question (≥ 20 chars) to the AI automatically.
- **Built-in OCR** — Detects screenshots copied to the clipboard and extracts text via optical character recognition.
- **Visual color indicator** — Each answer option (A, B, C, D…) has a configurable color. The tray icon changes instantly to the correct answer color.
- **Open-ended questions** — If no options are detected, the AI returns a full text answer.
- **Model auto-failover** — Automatically switches to the next candidate model when the current one is rate-limited or unavailable.
- **Session history** — Shows the last 10 answered questions with timestamp, answer, and question preview.
- **Customizable colors** — Change each option color (A-Z) using the native Windows color picker.
- **Multi-provider support** — Compatible with **Groq** (free tier) and **OpenAI** (GPT-4o).
- **Secure API Key storage** — Keys are stored in **Windows Credential Manager**, never in plaintext.

---

### 3. Requirements

- Windows 10 / 11 (64-bit)
- .NET 9 Runtime (bundled in the installer)
- Internet connection
- **Groq API Key** (free) **or** OpenAI API Key

---

### 4. API Key Setup

#### 🔑 Get a Groq API Key (FREE)

1. Go to **[console.groq.com](https://console.groq.com/)** and sign in or create a free account.
2. In the left panel, click **"API Keys"**.
3. Click **"+ Create API Key"**, enter a name (e.g. `CLIP-ANS`) and click Create.
4. **IMPORTANT:** Copy the full key immediately (starts with `gsk_...`). It is only shown once.

#### ⚙️ Enter the API Key in CLIP-ANS

1. Open CLIP-ANS (tray icon → right-click → Open).
2. Under **"AI Provider"**, select `GROQ`.
3. Paste your key in the **"API KEY"** field.
4. Click **"Test Connection"** to verify.
5. If successful, the configuration is saved automatically. ✅

---

### 5. Usage

#### Multiple-choice question
1. Copy the question + options (`Ctrl+C`) from anywhere.
2. The tray icon changes color in under 1 second → that color is the correct answer.

#### Screenshot (OCR)
1. Take a screenshot of the question (`Win+Shift+S`).
2. CLIP-ANS detects the image, extracts text via OCR and answers automatically.

#### Open-ended question
1. If no A/B/C/D options are detected, the AI returns a full text answer.
2. Visible in the **"Real-time status"** panel with a **Copy** button.

---

### Build & Run

```bash
# Run in development
dotnet run --project src\CLIP-ANS\CLIP-ANS.csproj

# Publish release binary
dotnet publish src\CLIP-ANS\CLIP-ANS.csproj -c Release -o .\publish\
```

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for details.
