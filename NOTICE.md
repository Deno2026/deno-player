# Third-party Notices

## mpv

This program uses **mpv** (https://mpv.io) as an external media playback
backend. mpv is licensed under GPLv2+ / LGPLv2.1+.

Deno Player does **not** redistribute mpv binaries or link mpv code into its
own executable. mpv runs as a separate process and Deno Player communicates
with it through a JSON IPC named pipe.

End users obtain mpv themselves (see `README.md` → "mpv 설치"). The bundled
`runtime/mpv/mpv.exe` location is reserved for user-supplied builds.

## Icons

Glyphs in the UI use the Windows-native **Segoe Fluent Icons** / **Segoe MDL2
Assets** fonts that ship with Windows 10/11. No external icon assets are
bundled.
