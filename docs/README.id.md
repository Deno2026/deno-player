# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Português (Brasil)](README.pt-BR.md) | Bahasa Indonesia

**Pemutar media Windows yang bersih untuk memeriksa video, audio, gambar, dan video bersubtitle dengan cepat.**

Tanpa iklan. Tanpa akun. Tanpa sinkronisasi cloud. Tanpa telemetri. Buka file dan langsung tonton.

![Deno Video Player preview](assets/preview.png)

## ✨ Kenapa Berguna

- Putar media lokal dengan drag-and-drop
- Otomatis membuat playlist sederhana dari folder yang sama
- Panel samping dengan hover untuk playlist dan file terbaru
- Simpan screenshot dengan `Ctrl + S`
- Potong klip secara sederhana dan lossless dengan `I` → `O` → `Ctrl + E`
- Kontrol fullscreen otomatis tersembunyi saat menonton
- Antarmuka aplikasi tersedia dalam bahasa Inggris dan Korea di Settings
- Update ditampilkan lebih dulu dan hanya dipasang saat kamu memilihnya

## 🚀 Instal dalam 3 Langkah

1. Unduh installer terbaru:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/latest/download/DenoVideoPlayer-win-Setup.exe)
2. Jalankan installer.
3. Buka **Deno Video Player**, lalu tarik file media ke jendela aplikasi.

Pada peluncuran pertama, aplikasi menyiapkan playback engine yang dibutuhkan. Biasanya ini hanya perlu dilakukan satu kali.

Jika Windows SmartScreen muncul, pastikan file berasal dari halaman resmi GitHub Releases, lalu pilih **More info** → **Run anyway**.

## 📦 Opsi Portable

Jika tidak ingin menginstal, unduh `portable-win-x64.zip` terbaru dari [Releases](https://github.com/Deno2026/deno-video-player/releases), ekstrak, lalu jalankan `DenoVideoPlayer.exe`.

Untuk kebanyakan pemula, installer `Setup.exe` adalah pilihan yang lebih mudah.

## 🎬 Yang Bisa Dilakukan

### Memeriksa Media dengan Cepat

Buka video, audio, gambar, atau video dengan subtitle tanpa manajemen library yang berat. Deno Video Player fokus pada pemeriksaan file lokal yang cepat.

### Menjelajah Folder yang Sama

Saat membuka satu file, aplikasi dapat memakai media lain di folder yang sama sebagai playlist sederhana. Ini berguna untuk memeriksa hasil render, export, referensi, dan klip unduhan.

### Memotong Tanpa Re-encode

1. Tekan `I` untuk menandai titik awal
2. Tekan `O` untuk menandai titik akhir
3. Tekan `Ctrl + E` untuk menyimpan klip

Fitur trim memakai FFmpeg stream copy, jadi cepat dan tidak melakukan re-encode. Titik awal dan akhir bisa bergeser sedikit ke keyframe terdekat.

## 🧩 File yang Didukung

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 Dukungan Bahasa

Antarmuka aplikasi saat ini mendukung:

- English
- Korean

Bahasa tampilan dapat diubah di **Settings**.

## ⌨️ Shortcut Praktis

| Aksi | Shortcut |
| --- | --- |
| Play / pause | `Space` |
| 2x saat ditahan | Tahan `Space` |
| Maju / mundur 5 detik | `←` / `→` |
| Maju / mundur 30 detik | `Shift + ←` / `Shift + →` |
| Volume | `↑` / `↓` atau roda mouse |
| Mute | `M` |
| Fullscreen | `F` / `F11` / `Enter` / `Alt + Enter` / double-click |
| Keluar fullscreen | `Esc` |
| File sebelumnya / berikutnya | `PageUp` / `PageDown` atau `Ctrl + ←` / `Ctrl + →` |
| Screenshot | `Ctrl + S` |
| Always on top | `Ctrl + T` |
| Playlist | `P` / `Ctrl + L` |
| Track subtitle | `V` / `Shift + V` |
| Track audio | `Ctrl + J` |
| Potong klip | `I` → `O` → `Ctrl + E` |

## 🔒 Yang Tidak Dilakukan

Deno Video Player sengaja menghindari fitur media library yang berat.

Tidak ada iklan, login, sinkronisasi cloud, analitik, rekomendasi, indexing library di background, store, marketplace plugin, editor timeline, atau fitur AI.

## 🗒️ Update

Lihat [CHANGELOG.md](../CHANGELOG.md) untuk perubahan terbaru.

## 🛠️ Catatan Developer

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 Lisensi

Source code Deno Video Player dirilis dengan [The Unlicense](../LICENSE). Kamu bebas menggunakan, menyalin, memodifikasi, menerbitkan, mendistribusikan, dan menggunakannya secara komersial.

Tool pihak ketiga seperti mpv, FFmpeg, Velopack, dan 7-Zip tetap mengikuti lisensi masing-masing. Lihat [NOTICE.md](../NOTICE.md) untuk detail.
