# Deno Video Player

[English](../README.md) | 한국어 | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**로컬 영상, 오디오, 이미지, 자막 파일을 빠르게 확인하는 깔끔한 Windows 미디어 플레이어입니다.**

광고 없음. 계정 없음. 클라우드 동기화 없음. 텔레메트리 없음. 파일을 열고 바로 확인하세요.

![Deno Video Player preview](assets/preview.png)

## ✨ 이런 점이 좋아요

- 로컬 미디어 파일을 드래그 앤 드롭으로 빠르게 재생
- 같은 폴더의 미디어를 자동으로 간단한 재생목록으로 구성
- 화면 양쪽 가장자리 hover로 재생목록과 최근 파일 확인
- `Ctrl + S`로 스크린샷 저장
- `I` → `O` → `Ctrl + E`로 간단한 무손실 구간 추출
- 시청 중에는 전체화면 컨트롤이 깔끔하게 숨겨짐
- 설정에서 영어/한국어 표시 언어 선택
- 업데이트는 먼저 안내하고, 사용자가 선택할 때만 설치

## 🚀 3단계 설치

1. 최신 설치 파일을 받으세요:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/latest/download/DenoVideoPlayer-win-Setup.exe)
2. 설치 파일을 실행하세요.
3. **Deno Video Player**를 열고 미디어 파일을 창에 끌어다 놓으세요.

첫 실행 때 필요한 재생 엔진을 준비합니다. 보통 처음 한 번만 진행됩니다.

Windows SmartScreen이 보이면 공식 GitHub Releases에서 받은 파일인지 확인한 뒤 **More info** → **Run anyway**로 진행하면 됩니다.

## 📦 포터블 버전

설치 없이 쓰고 싶다면 [Releases](https://github.com/Deno2026/deno-video-player/releases)에서 최신 `portable-win-x64.zip`을 받아 압축을 풀고 `DenoVideoPlayer.exe`를 실행하세요.

초보자에게는 `Setup.exe` 설치 버전을 추천합니다.

## 🎬 주요 기능

### 빠른 미디어 확인

영상, 오디오, 이미지, 자막이 있는 영상을 빠르게 열 수 있습니다. Deno Video Player는 무거운 라이브러리 관리보다 로컬 파일 확인에 집중합니다.

### 같은 폴더 탐색

파일 하나를 열면 같은 폴더의 주변 미디어를 간단한 재생목록처럼 사용할 수 있습니다. 렌더 결과, 다운로드 클립, 레퍼런스 파일을 확인할 때 편합니다.

### 재인코딩 없는 구간 추출

1. `I`로 시작점 지정
2. `O`로 끝점 지정
3. `Ctrl + E`로 저장

FFmpeg stream copy 방식이라 빠르고 화질 손실이 없습니다. 다만 키프레임 기준으로 저장되기 때문에 시작/끝 지점이 보이는 프레임과 조금 달라질 수 있습니다.

## 🧩 지원 파일

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 언어 지원

앱 내부 표시 언어는 현재 다음을 지원합니다.

- English
- 한국어

언어는 **Settings**에서 변경할 수 있습니다.

## ⌨️ 자주 쓰는 단축키

| 기능 | 단축키 |
| --- | --- |
| 재생 / 일시정지 | `Space` |
| 누르고 있는 동안 2배속 | `Space` 길게 누르기 |
| 5초 이동 | `←` / `→` |
| 30초 이동 | `Shift + ←` / `Shift + →` |
| 볼륨 | `↑` / `↓` 또는 마우스 휠 |
| 음소거 | `M` |
| 전체화면 | `F` / `F11` / `Enter` / `Alt + Enter` / 더블클릭 |
| 전체화면 해제 | `Esc` |
| 이전 / 다음 파일 | `PageUp` / `PageDown` 또는 `Ctrl + ←` / `Ctrl + →` |
| 스크린샷 | `Ctrl + S` |
| 항상 위 | `Ctrl + T` |
| 재생목록 | `P` / `Ctrl + L` |
| 자막 트랙 | `V` / `Shift + V` |
| 오디오 트랙 | `Ctrl + J` |
| 구간 추출 | `I` → `O` → `Ctrl + E` |

## 🔒 하지 않는 것

Deno Video Player는 무거운 미디어 라이브러리 기능을 의도적으로 넣지 않습니다.

광고, 로그인, 클라우드 동기화, 분석, 추천, 백그라운드 라이브러리 인덱싱, 스토어, 플러그인 마켓, 타임라인 편집기, AI 기능이 없습니다.

## 🗒️ 업데이트 내역

최근 변경 사항은 [CHANGELOG.md](../CHANGELOG.md)를 확인하세요.

## 🛠️ 개발자 참고

```powershell
dotnet restore DenoVideoPlayer.sln
dotnet test .\DenoVideoPlayer.sln --configuration Release
dotnet publish .\DenoVideoPlayer.csproj -c Release -r win-x64 --self-contained true -o .\publish\DenoVideoPlayer-win-x64
```

## 🧾 라이선스

Deno Video Player 소스 코드는 [GNU GPL v3.0](../LICENSE) (`GPL-3.0-only`)으로 배포됩니다. 사용, 학습, 수정, 재배포, 상업적 이용이 가능합니다. 수정본을 배포할 때는 GPL-3.0을 따라야 하며 필요한 라이선스와 저작권 고지를 유지해야 합니다.

mpv, FFmpeg, Velopack, 7-Zip 같은 외부 도구는 각자의 라이선스를 따릅니다. 자세한 내용은 [NOTICE.md](../NOTICE.md)를 확인하세요.
