# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Português (Portugal)](README.pt-PT.md) | [Português (Brasil)](README.pt-BR.md) | Bahasa Indonesia

**Pemutar media Windows yang bersih untuk memeriksa video, audio, gambar, dan video bersubtitle dengan cepat.**

Tanpa iklan. Tanpa akun. Tanpa sinkronisasi cloud. Tanpa telemetri. Buka file dan langsung tonton.

**Versi stabil terbaru:** [v0.5.4](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.4) · Dirilis 7 September 2026

![Deno Video Player memutar video contoh dengan subtitle](assets/playback-preview.png)

*Pemutaran nyata video contoh buatan DENO dengan subtitle. Antarmuka aplikasi tersedia dalam bahasa Inggris dan Korea. [Layar saat pertama kali dijalankan](assets/preview.png).*

## ✨ Kenapa Berguna

- Putar media lokal dengan drag-and-drop
- Otomatis membuat playlist sederhana dari folder yang sama
- Tombol toolbar dan pegangan tipis di tengah tepi kiri/kanan untuk file terbaru dan playlist
- Simpan screenshot dengan `Ctrl + S`
- Zoom video dengan `Ctrl + roda mouse`, lalu geser dengan drag tombol tengah
- Potong klip secara sederhana dan lossless dengan `I` → `O` → `Ctrl + E`
- Tombol fullscreen khusus untuk menampilkan video saja
- Panduan singkat bawaan dan referensi shortcut praktis dengan `F1`
- Antarmuka aplikasi tersedia dalam bahasa Inggris dan Korea di Settings
- Update ditampilkan lebih dulu dan hanya dipasang saat kamu memilihnya

## 🚀 Instal dalam 3 Langkah

1. Unduh installer terbaru:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-win-Setup.exe)
2. Jalankan installer.
3. Buka **Deno Video Player**, lalu tarik file media ke jendela aplikasi.

Pada peluncuran pertama, aplikasi menyiapkan playback engine yang dibutuhkan. Biasanya ini hanya perlu dilakukan satu kali.

Jika Windows SmartScreen muncul, pastikan file berasal dari halaman resmi GitHub Releases, lalu pilih **More info** → **Run anyway**.

### Persyaratan sistem

- Windows 10 atau Windows 11, x64
- Akses internet pada peluncuran pertama untuk menyiapkan playback engine mpv
- Akses internet pada ekspor klip, audio, atau video pertama untuk menyiapkan FFmpeg

## ❓ Panduan bawaan

Klik tombol `?` di sebelah Settings atau tekan `F1` kapan saja. Panduan ini menjelaskan:

- lokasi panel file terbaru dan playlist folder saat ini
- tool utama di bar atas dan kontrol playback
- shortcut keyboard dan mouse
- subtitle, track audio, screenshot, zoom, dan geser
- ekspor klip, audio saja, dan video saja
- pemecahan masalah peluncuran pertama dan playback

Player yang kosong juga menyediakan link untuk mulai menggunakan aplikasi, dan kegagalan playback langsung mengarah ke bagian pemecahan masalah.

## 📦 Opsi Portable

Jika tidak ingin menginstal, unduh [DenoVideoPlayer-v0.5.4-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-v0.5.4-portable-win-x64.zip), ekstrak, lalu jalankan `DenoVideoPlayer.exe`.

Untuk kebanyakan pemula, installer `Setup.exe` adalah pilihan yang lebih mudah.

## 🎬 Yang Bisa Dilakukan

### Memeriksa Media dengan Cepat

Buka video, audio, gambar, atau video dengan subtitle tanpa manajemen library yang berat. Deno Video Player fokus pada pemeriksaan file lokal yang cepat.

`F`, klik dua kali area tampilan atau bilah judul, dan tombol ukuran di kanan atas/bawah memiliki tindakan yang sama: jendela normal masuk ke fullscreen; fullscreen atau jendela yang dimaksimalkan oleh Windows kembali ke ukuran normal sebelumnya. Ini juga berlaku saat player kosong, sedang memuat, atau setelah playback gagal. Untuk membuka media, gunakan **Buka file** / **Buka folder**, `Ctrl + O`, atau seret dan lepas.

### Menjelajah Folder yang Sama

Saat membuka satu file, aplikasi dapat memakai media lain di folder yang sama sebagai playlist sederhana. Ini berguna untuk memeriksa hasil render, export, referensi, dan klip unduhan.

Buka file terbaru dari toolbar atau dengan `Ctrl + H`. Buka playlist folder saat ini dari tombol toolbar atau dengan `P` / `Ctrl + L`. Urutkan menurut urutan alami nama file, terbaru lebih dulu, atau terlama lebih dulu; urutan yang dipilih akan diingat dan juga mengatur navigasi file sebelumnya atau berikutnya.

Untuk melihat sekilas, arahkan kursor ke pegangan tipis di tengah tepi kiri untuk file terbaru, atau tepi kanan untuk playlist. Panel pop-up menutup saat kursor meninggalkan pegangan dan panel. Panel yang dibuka melalui tombol toolbar atau pintasan tetap terbuka sampai Anda menutup atau beralih panel.

Kontrol bawah mencakup pengulangan nonaktif, ulangi semua, ulangi satu, shuffle, volume, kecepatan playback, dan fullscreen. Klik nilai kecepatan untuk membuka preset atau putar roda mouse di atasnya untuk perubahan 0,25x.

### Memotong Tanpa Re-encode

1. Tekan `I` untuk menandai titik awal
2. Tekan `O` untuk menandai titik akhir
3. Tekan `Ctrl + E` untuk menyimpan klip

Fitur trim memakai FFmpeg stream copy, jadi cepat dan tidak melakukan re-encode. Titik awal dan akhir bisa bergeser sedikit ke keyframe terdekat.

Dalam mode edit, kamu dapat menyimpan klip penuh, mengekstrak audio saja, atau video saja.
FFmpeg disiapkan saat ekspor pertama bila diperlukan, dan ukuran unduhannya bisa besar.

## 🧩 File yang Didukung

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
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
| Zoom / geser video | `Ctrl + roda mouse` / drag tombol tengah |
| Mute | `M` |
| Fullscreen / pulihkan ukuran jendela | `F` / `F11` / `Enter` / `Alt + Enter` / klik dua kali area tampilan atau bilah judul |
| Keluar fullscreen | `Esc` |
| File sebelumnya / berikutnya | `PageUp` / `PageDown` atau `Ctrl + ←` / `Ctrl + →` |
| Screenshot | `Ctrl + S` |
| Always on top | `Ctrl + T` |
| File terbaru | `Ctrl + H` |
| Playlist | `P` / `Ctrl + L` |
| Track subtitle | `V` / `Shift + V` |
| Track audio | `Ctrl + J` |
| Potong klip | `I` → `O` → `Ctrl + E` |
| Panduan dan shortcut | `F1` |

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

Source code Deno Video Player dirilis dengan [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`). Kamu dapat menggunakan, mempelajari, memodifikasi, dan mendistribusikannya kembali, termasuk untuk penggunaan komersial. Versi modifikasi yang kamu distribusikan harus mengikuti GPL-3.0 dan mempertahankan pemberitahuan lisensi serta hak cipta yang diperlukan.

Tool pihak ketiga seperti mpv, FFmpeg, Velopack, dan 7-Zip tetap mengikuti lisensi masing-masing. Lihat [NOTICE.md](../NOTICE.md) untuk detail.
