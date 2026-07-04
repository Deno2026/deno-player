# Third-party Notices

Deno Video Player source code is released under GNU GPL v3.0 (`GPL-3.0-only`). Third-party
tools and libraries listed below keep their own licenses.

## mpv

This program uses **mpv** (https://mpv.io) as an external media playback
backend. mpv is licensed under GPLv2+ / LGPLv2.1+.

Deno Video Player does **not** redistribute mpv binaries or link mpv code into its
own executable. mpv runs as a separate process and Deno Video Player communicates
with it through a JSON IPC named pipe.

The app can prepare mpv on first launch by downloading a Windows build into
`runtime/mpv/`. The bundled `runtime/mpv/mpv.exe` location is reserved for this
user-side downloaded build.

## FFmpeg

The trim feature can use **FFmpeg** (https://ffmpeg.org/) as an external command
line tool for stream-copy clipping. Deno Video Player does not redistribute
FFmpeg binaries in the source repository. The app or `START_HERE.bat` can prepare
FFmpeg on the user's machine by downloading it into `runtime/ffmpeg/`.

## Icons

Glyphs in the UI use the Windows-native **Segoe Fluent Icons** / **Segoe MDL2
Assets** fonts that ship with Windows 10/11. No external icon assets are
bundled.

## 7-Zip (7zr.exe)

`tools/7zr.exe` is the **7-Zip standalone command-line tool** (https://www.7-zip.org)
licensed under LGPL-2.1+. It is included so `tools/fetch-mpv.ps1` can extract
mpv's 7z release archive without requiring the user to install 7-Zip first.

7-Zip source: https://www.7-zip.org/download.html
