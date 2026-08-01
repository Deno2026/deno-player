# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | Español | [Português (Portugal)](README.pt-PT.md) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**Un reproductor multimedia limpio para Windows, pensado para revisar rápidamente videos, audio, imágenes y videos con subtítulos.**

Sin anuncios. Sin cuenta. Sin sincronización en la nube. Sin telemetría. Abre un archivo y empieza a verlo.

**Versión estable más reciente:** [v0.5.3](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.3) · Publicada el 2 de agosto de 2026

![Deno Video Player preview](assets/preview.png)

*La captura se muestra en coreano. La interfaz de la aplicación está disponible en inglés y coreano.*

## ✨ Por qué es útil

- Reproduce archivos locales con solo arrastrar y soltar
- Crea automáticamente una lista sencilla con los medios de la misma carpeta
- Botones en la barra superior para archivos recientes y lista de reproducción
- Capturas de pantalla con `Ctrl + S`
- Zoom de video con `Ctrl + rueda del mouse` y desplazamiento arrastrando el botón central
- Recorte simple y sin pérdida con `I` → `O` → `Ctrl + E`
- Un botón de pantalla completa para dejar solo el video visible
- Una guía rápida integrada y una referencia práctica de atajos con `F1`
- Interfaz de la app en inglés y coreano desde Settings
- Las actualizaciones se muestran primero y solo se instalan cuando tú lo decides

## 🚀 Instalación en 3 pasos

1. Descarga el instalador más reciente:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-win-Setup.exe)
2. Ejecuta el instalador.
3. Abre **Deno Video Player** y arrastra un archivo multimedia a la ventana.

En el primer inicio, la app prepara el motor de reproducción que necesita. Normalmente se hace solo una vez.

Si aparece Windows SmartScreen, confirma que el archivo viene de la página oficial de GitHub Releases y elige **More info** → **Run anyway**.

### Requisitos del sistema

- Windows 10 o Windows 11, x64
- Acceso a internet en el primer inicio para preparar el motor de reproducción mpv
- Acceso a internet en la primera exportación de un clip, audio o video para preparar FFmpeg

## ❓ Guía integrada

Haz clic en el botón `?` junto a Settings o presiona `F1` en cualquier momento. La guía explica:

- dónde están los paneles de archivos recientes y de la lista de reproducción de la carpeta actual
- las herramientas principales de la barra superior y los controles de reproducción
- los atajos de teclado y mouse
- subtítulos, pistas de audio, capturas de pantalla, zoom y desplazamiento
- la exportación de clips, solo audio y solo video
- la solución de problemas del primer inicio y de reproducción

El reproductor vacío también incluye un enlace de primeros pasos, y los errores de reproducción llevan directamente a la sección de solución de problemas.

## 📦 Opción portable

Si prefieres no instalar, descarga [DenoVideoPlayer-v0.5.3-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-v0.5.3-portable-win-x64.zip), descomprímelo y ejecuta `DenoVideoPlayer.exe`.

Para la mayoría de principiantes, el instalador `Setup.exe` es la opción más sencilla.

## 🎬 Qué puedes hacer

### Revisar medios rápidamente

Abre videos, audio, imágenes o videos con subtítulos sin convertirlos en una biblioteca pesada. Deno Video Player está pensado para revisar archivos locales con rapidez.

Haz doble clic en el contenido multimedia abierto para entrar o salir de pantalla completa. Cuando el reproductor está vacío, el doble clic abre el selector de archivos; mientras se carga el contenido o después de un fallo de reproducción, no hace nada.

### Explorar la misma carpeta

Al abrir un archivo, la app puede usar los otros medios cercanos de la misma carpeta como una lista simple. Es útil para revisar renders, exportaciones, referencias y clips descargados.

Abre los archivos recientes desde la barra superior o con `Ctrl + H`. Abre la lista de reproducción de la carpeta actual desde su botón o con `P` / `Ctrl + L`. Ordénala por nombre de archivo con orden natural, más recientes primero o más antiguos primero; el orden elegido se recuerda y también controla la navegación al archivo anterior o siguiente.

Los controles inferiores incluyen repetición desactivada, repetir todo, repetir uno, reproducción aleatoria, volumen, velocidad de reproducción y pantalla completa. Haz clic en el valor de velocidad para ver los ajustes predefinidos o usa la rueda del mouse sobre él para cambiar en pasos de 0,25x.

### Recortar sin recodificar

1. Presiona `I` para marcar el inicio
2. Presiona `O` para marcar el final
3. Presiona `Ctrl + E` para guardar el clip

El recorte usa FFmpeg stream copy, así que es rápido y no recodifica el video. El inicio y el final pueden ajustarse al keyframe más cercano.

En el modo de edición puedes guardar el clip completo, extraer solo el audio o extraer solo el video.
FFmpeg se prepara bajo demanda en la primera exportación y la descarga puede ser grande.

## 🧩 Archivos compatibles

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 Idiomas

La interfaz de la app actualmente admite:

- English
- Korean

Puedes cambiar el idioma en **Settings**.

## ⌨️ Atajos útiles

| Acción | Atajo |
| --- | --- |
| Reproducir / pausar | `Space` |
| 2x mientras mantienes presionado | Mantener `Space` |
| Avanzar / retroceder 5 segundos | `←` / `→` |
| Avanzar / retroceder 30 segundos | `Shift + ←` / `Shift + →` |
| Volumen | `↑` / `↓` o rueda del mouse |
| Zoom / desplazamiento de video | `Ctrl + rueda del mouse` / arrastrar con botón central |
| Silenciar | `M` |
| Pantalla completa | `F` / `F11` / `Enter` / `Alt + Enter` / doble clic en el contenido multimedia abierto |
| Salir de pantalla completa | `Esc` |
| Archivo anterior / siguiente | `PageUp` / `PageDown` o `Ctrl + ←` / `Ctrl + →` |
| Captura de pantalla | `Ctrl + S` |
| Siempre visible | `Ctrl + T` |
| Archivos recientes | `Ctrl + H` |
| Lista de reproducción | `P` / `Ctrl + L` |
| Pista de subtítulos | `V` / `Shift + V` |
| Pista de audio | `Ctrl + J` |
| Recortar clip | `I` → `O` → `Ctrl + E` |
| Guía y atajos | `F1` |

## 🔒 Lo que no hace

Deno Video Player evita a propósito las funciones pesadas de biblioteca multimedia.

No tiene anuncios, inicio de sesión, nube, analíticas, recomendaciones, indexación en segundo plano, tienda, marketplace de plugins, editor de línea de tiempo ni funciones de IA.

## 🗒️ Cambios

Consulta [CHANGELOG.md](../CHANGELOG.md) para ver los cambios recientes.

## 🛠️ Para desarrolladores

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 Licencia

El código fuente de Deno Video Player se publica bajo [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`). Puedes usarlo, estudiarlo, modificarlo y redistribuirlo, incluso comercialmente. Las versiones modificadas que distribuyas deben seguir GPL-3.0 y conservar los avisos de licencia y copyright requeridos.

Herramientas de terceros como mpv, FFmpeg, Velopack y 7-Zip mantienen sus propias licencias. Consulta [NOTICE.md](../NOTICE.md) para más detalles.
