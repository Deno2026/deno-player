# Deno Video Player

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | 简体中文 | [Español](README.es.md) | [Português (Portugal)](README.pt-PT.md) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**一个简洁的 Windows 本地媒体播放器，用来快速查看视频、音频、图片和字幕视频。**

无广告。无需账号。无云同步。无遥测。打开文件，即可观看。

**最新稳定版:** [v0.5.4](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.4) · 2026 年 9 月 7 日发布

![Deno Video Player 播放示例视频和字幕的画面](assets/playback-preview.png)

*实际播放 DENO 制作的示例视频和字幕的画面。应用界面支持英语和韩语。[查看首次启动画面](assets/preview.png)。*

## ✨ 为什么好用

- 拖放本地媒体文件即可快速播放
- 自动把同一文件夹中的媒体整理成简单播放列表
- 通过工具栏按钮或左右边缘中央的细手柄打开最近文件和播放列表
- 使用 `Ctrl + S` 保存截图
- 使用 `Ctrl + 鼠标滚轮` 缩放视频，并按住中键拖动
- 使用 `I` → `O` → `Ctrl + E` 快速无损裁剪片段
- 使用全屏专用按钮隐藏操作界面，只显示视频
- 使用 `F1` 打开内置快速指南和实用快捷键说明
- 可在设置中选择英文或韩文界面
- 更新会先提示，只有在你选择后才会安装

## 🚀 3 步安装

1. 下载最新安装程序：
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-win-Setup.exe)
2. 运行安装程序。
3. 打开 **Deno Video Player**，把媒体文件拖到窗口中。

首次启动时，应用会准备所需的播放引擎。通常只需要进行一次。

如果 Windows SmartScreen 弹出提示，请确认文件来自官方 GitHub Releases 页面，然后选择 **More info** → **Run anyway**。

### 系统要求

- Windows 10 或 Windows 11，x64
- 首次启动需要联网，以准备 mpv 播放引擎
- 首次导出片段、音频或视频需要联网，以准备 FFmpeg

## ❓ 内置使用指南

随时单击设置旁的 `?` 按钮或按 `F1`。指南涵盖：

- 最近文件和当前文件夹播放列表面板的位置
- 顶部栏的主要工具和播放控件
- 键盘和鼠标快捷键
- 字幕、音频轨道、截图、缩放和移动
- 导出片段、仅音频和仅视频
- 首次启动和播放问题排查

空白播放器还提供初次使用链接，播放失败时则会直接链接到问题排查部分。

## 📦 便携版

如果不想安装，可以下载 [DenoVideoPlayer-v0.5.4-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-v0.5.4-portable-win-x64.zip)，解压后运行 `DenoVideoPlayer.exe`。

对大多数新手来说，`Setup.exe` 安装版更简单。

## 🎬 主要用途

### 快速查看媒体

可以快速打开视频、音频、图片和带字幕的视频。Deno Video Player 专注于本地文件查看，而不是复杂的媒体库管理。

`F`、双击显示区域或标题栏，以及右上角和右下角的窗口大小按钮，操作完全一致：普通窗口进入全屏；全屏或 Windows 最大化窗口恢复到之前的普通窗口大小。空白画面、加载中或播放失败时也一样。要打开媒体，请使用**打开文件** / **打开文件夹**、`Ctrl + O` 或拖放。

### 查看同一文件夹内容

打开一个文件后，应用可以把同一文件夹中的附近媒体作为简单播放列表使用。适合检查渲染结果、导出视频、参考素材和下载片段。

通过工具栏按钮或 `Ctrl + H` 打开最近文件。通过工具栏按钮或 `P` / `Ctrl + L` 打开当前文件夹播放列表。可按文件名自然排序、最新优先或最早优先；所选顺序会被保留，并用于上一个 / 下一个文件的导航顺序。

需要快速查看时，将鼠标悬停在左侧边缘中央的细手柄上可打开最近文件，右侧手柄可打开播放列表。鼠标离开手柄和面板后，弹出面板会关闭。通过工具栏按钮或快捷键打开的面板会保持显示，直到手动关闭或切换面板。

底部控件包括关闭循环、全部循环、单项循环、随机播放、音量、播放速度和全屏。单击速度值可打开预设，也可以在速度值上滚动鼠标滚轮，以 0.25x 为单位调整。

### 无需重新编码即可裁剪片段

1. 按 `I` 设置开始点
2. 按 `O` 设置结束点
3. 按 `Ctrl + E` 保存片段

裁剪使用 FFmpeg stream copy，因此速度快且不会重新编码。由于按关键帧处理，开始和结束位置可能会与画面中看到的精确帧略有不同。

在编辑模式中，可以保存完整片段、仅提取音频或仅提取视频。
首次导出时会按需准备 FFmpeg，下载文件可能较大。

## 🧩 支持的文件

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 语言支持

应用界面目前支持：

- English
- Korean

你可以在 **Settings** 中更改显示语言。

## ⌨️ 常用快捷键

| 操作 | 快捷键 |
| --- | --- |
| 播放 / 暂停 | `Space` |
| 按住时 2 倍速 | 按住 `Space` |
| 前后移动 5 秒 | `←` / `→` |
| 前后移动 30 秒 | `Shift + ←` / `Shift + →` |
| 音量 | `↑` / `↓` 或鼠标滚轮 |
| 视频缩放 / 移动 | `Ctrl + 鼠标滚轮` / 按住中键拖动 |
| 静音 | `M` |
| 全屏 / 恢复窗口大小 | `F` / `F11` / `Enter` / `Alt + Enter` / 双击显示区域或标题栏 |
| 退出全屏 | `Esc` |
| 上一个 / 下一个文件 | `PageUp` / `PageDown` 或 `Ctrl + ←` / `Ctrl + →` |
| 截图 | `Ctrl + S` |
| 置顶 | `Ctrl + T` |
| 最近文件 | `Ctrl + H` |
| 播放列表 | `P` / `Ctrl + L` |
| 字幕轨道 | `V` / `Shift + V` |
| 音频轨道 | `Ctrl + J` |
| 裁剪片段 | `I` → `O` → `Ctrl + E` |
| 使用指南和快捷键 | `F1` |

## 🔒 它不会做什么

Deno Video Player 有意避免复杂的媒体库功能。

它没有广告、登录、云同步、分析、推荐、后台库索引、商店、插件市场、时间线编辑器或 AI 功能。

## 🗒️ 更新记录

最近的变更请查看 [CHANGELOG.md](../CHANGELOG.md)。

## 🛠️ 开发者说明

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 许可证

Deno Video Player 源代码基于 [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`) 发布。你可以使用、学习、修改和再分发，也可以用于商业用途。分发修改版本时必须遵循 GPL-3.0，并保留所需的许可证和版权声明。

mpv、FFmpeg、Velopack、7-Zip 等第三方工具仍遵循各自的许可证。详情请查看 [NOTICE.md](../NOTICE.md)。
