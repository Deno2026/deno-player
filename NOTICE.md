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
FFmpeg binaries in the source repository. The app prepares FFmpeg on demand for
the first export by downloading it into the persistent user runtime directory.

## Icons

Glyphs in the UI use the Windows-native **Segoe Fluent Icons** / **Segoe MDL2
Assets** fonts that ship with Windows 10/11. The dedicated media `.ico` files in
`Assets/Icons/` are rendered image assets; no font files are bundled.

## .NET runtime

Self-contained packages include the Microsoft .NET runtime and Windows Desktop
runtime components required to run the application. Their authoritative license
and third-party notice files are copied from the exact resolved runtime packs into
`licenses/dotnet/` during publish.

## Velopack

The installed application includes **Velopack** (https://github.com/velopack/velopack),
licensed under the MIT License.

Copyright © 2021 Caelan Sayler
Copyright © 2024 Velopack Ltd.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## 7-Zip (7zr.exe)

`tools/7zr.exe` is the reduced 7z command-line tool from the **LZMA SDK**
(https://www.7-zip.org/sdk.html). The LZMA SDK is in the public domain. It is
included so `tools/fetch-mpv.ps1` can extract mpv's 7z release archive without
requiring the user to install 7-Zip first.

LZMA SDK source: https://www.7-zip.org/sdk.html
