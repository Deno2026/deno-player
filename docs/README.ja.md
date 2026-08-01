# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | 日本語 | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Português (Portugal)](README.pt-PT.md) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**ローカルの動画、音声、画像、字幕付き動画をすばやく確認できる、Windows 向けの軽量メディアプレイヤーです。**

広告なし。アカウント不要。クラウド同期なし。テレメトリなし。ファイルを開いて、すぐに確認できます。

**最新の安定版:** [v0.5.3](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.3) · 2026 年 8 月 2 日公開

![Deno Video Player preview](assets/preview.png)

*画面は韓国語表示です。アプリのインターフェースは英語と韓国語に対応しています。*

## ✨ 便利なポイント

- ローカルメディアをドラッグ＆ドロップですばやく再生
- 同じフォルダ内のメディアを自動で簡易プレイリスト化
- ツールバーボタンで最近使ったファイルやプレイリストを表示
- `Ctrl + S` でスクリーンショット保存
- `Ctrl + マウスホイール` で動画を拡大し、中ボタンドラッグで移動
- `I` → `O` → `Ctrl + E` で簡単なロスレス切り出し
- フルスクリーン専用ボタンで操作 UI を隠し、映像だけを表示
- `F1` で開ける内蔵クイックガイドと実用的なショートカット一覧
- 設定から英語 / 韓国語の表示言語を選択
- アップデートは確認後、ユーザーが選んだときだけ適用

## 🚀 3 ステップでインストール

1. 最新のインストーラーをダウンロード:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-win-Setup.exe)
2. インストーラーを実行します。
3. **Deno Video Player** を開き、メディアファイルをウィンドウにドラッグ＆ドロップします。

初回起動時に必要な再生エンジンを準備します。通常は最初の 1 回だけです。

Windows SmartScreen が表示された場合は、公式 GitHub Releases から入手したファイルであることを確認し、**More info** → **Run anyway** を選んでください。

### システム要件

- Windows 10 または Windows 11、x64
- 初回起動時に mpv 再生エンジンを準備するためのインターネット接続
- 最初のクリップ / 音声 / 映像の書き出し時に FFmpeg を準備するためのインターネット接続

## ❓ アプリ内ガイド

設定の横にある `?` ボタンをクリックするか、いつでも `F1` を押してください。ガイドでは次の内容を確認できます。

- 最近使ったファイルと現在のフォルダのプレイリストパネルの場所
- 上部バーの主なツールと再生コントロール
- キーボードとマウスのショートカット
- 字幕、音声トラック、スクリーンショット、拡大、移動
- クリップ、音声のみ、映像のみの書き出し
- 初回起動と再生のトラブルシューティング

空のプレイヤーには初回利用向けのリンクもあり、再生に失敗した場合はトラブルシューティング項目へ直接移動できます。

## 📦 ポータブル版

インストールせずに使いたい場合は、[DenoVideoPlayer-v0.5.3-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.3/DenoVideoPlayer-v0.5.3-portable-win-x64.zip) をダウンロードし、解凍して `DenoVideoPlayer.exe` を実行してください。

はじめて使う方には `Setup.exe` 版がおすすめです。

## 🎬 主な使い方

### すばやくメディアを確認

動画、音声、画像、字幕付き動画をすぐに開けます。Deno Video Player は重いライブラリ管理ではなく、ローカルファイルの確認に集中しています。

開いているメディアをダブルクリックすると、フルスクリーンを切り替えられます。プレイヤーが空のときはファイル選択ダイアログが開き、メディアの読み込み中または再生失敗後は何も起こりません。

### 同じフォルダをそのまま確認

1 つのファイルを開くと、同じフォルダにあるメディアを簡易プレイリストとして扱えます。レンダー結果、書き出し動画、参考素材の確認に便利です。

最近使ったファイルはツールバーボタンまたは `Ctrl + H` で開けます。現在のフォルダのプレイリストはツールバーボタンまたは `P` / `Ctrl + L` で開きます。ファイル名の自然順、新しい順、古い順で並べ替えでき、選んだ順序は次回も保持され、前 / 次のファイルへの移動順にも反映されます。

下部のコントロールでは、リピートなし、全体リピート、1 項目リピート、シャッフル、音量、再生速度、フルスクリーンを操作できます。速度の値をクリックするとプリセットが開き、その上でマウスホイールを回すと 0.25x 刻みで変更できます。

### 再エンコードなしでクリップを切り出し

1. `I` で開始点を指定
2. `O` で終了点を指定
3. `Ctrl + E` で保存

FFmpeg の stream copy を使うため、高速で画質劣化もありません。ただしキーフレーム単位のため、開始/終了位置が見た目のフレームから少しずれることがあります。

編集モードでは、クリップ全体、音声のみ、映像のみのいずれかを保存できます。
初回の書き出し時に FFmpeg を必要に応じて準備します。ダウンロード容量が大きい場合があります。

## 🧩 対応ファイル

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 言語サポート

アプリの表示言語は現在、次に対応しています。

- English
- Korean

言語は **Settings** から変更できます。

## ⌨️ よく使うショートカット

| 操作 | ショートカット |
| --- | --- |
| 再生 / 一時停止 | `Space` |
| 押している間だけ 2 倍速 | `Space` 長押し |
| 5 秒移動 | `←` / `→` |
| 30 秒移動 | `Shift + ←` / `Shift + →` |
| 音量 | `↑` / `↓` またはマウスホイール |
| 動画の拡大 / 移動 | `Ctrl + マウスホイール` / 中ボタンドラッグ |
| ミュート | `M` |
| フルスクリーン | `F` / `F11` / `Enter` / `Alt + Enter` / 開いているメディアをダブルクリック |
| フルスクリーン解除 | `Esc` |
| 前 / 次のファイル | `PageUp` / `PageDown` または `Ctrl + ←` / `Ctrl + →` |
| スクリーンショット | `Ctrl + S` |
| 常に手前 | `Ctrl + T` |
| 最近使ったファイル | `Ctrl + H` |
| プレイリスト | `P` / `Ctrl + L` |
| 字幕トラック | `V` / `Shift + V` |
| 音声トラック | `Ctrl + J` |
| クリップ切り出し | `I` → `O` → `Ctrl + E` |
| ガイドとショートカット | `F1` |

## 🔒 入れていない機能

Deno Video Player は、重いメディアライブラリ機能を意図的に避けています。

広告、ログイン、クラウド同期、分析、レコメンド、バックグラウンド索引作成、ストア、プラグインマーケット、タイムライン編集、AI 機能はありません。

## 🗒️ 更新履歴

最近の変更は [CHANGELOG.md](../CHANGELOG.md) を確認してください。

## 🛠️ 開発者向け

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 ライセンス

Deno Video Player のソースコードは [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`) で公開されています。利用、学習、変更、再配布、商用利用ができます。変更版を配布する場合は GPL-3.0 に従い、必要なライセンス表示と著作権表示を保持してください。

mpv、FFmpeg、Velopack、7-Zip などの外部ツールは、それぞれのライセンスに従います。詳細は [NOTICE.md](../NOTICE.md) を確認してください。
