# Deno Video Player

[English](../README.md) | 한국어 | [日本語](README.ja.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Português (Portugal)](README.pt-PT.md) | [Português (Brasil)](README.pt-BR.md) | [Bahasa Indonesia](README.id.md)

**로컬 영상, 오디오, 이미지, 자막 파일을 빠르게 확인하는 깔끔한 Windows 미디어 플레이어입니다.**

광고 없음. 계정 없음. 클라우드 동기화 없음. 텔레메트리 없음. 파일을 열고 바로 확인하세요.

**최신 안정 버전:** [v0.5.4](https://github.com/Deno2026/deno-video-player/releases/tag/v0.5.4) · 2026년 9월 7일 공개

![Deno Video Player에서 샘플 영상과 자막을 재생하는 화면](assets/playback-preview.png)

*DENO가 만든 샘플 영상과 자막을 실제로 재생한 화면입니다. 앱 UI는 한국어와 영어를 지원합니다. [첫 실행 화면 보기](assets/preview.png).*

## ✨ 이런 점이 좋아요

- 로컬 미디어 파일을 드래그 앤 드롭으로 빠르게 재생
- 같은 폴더의 미디어를 자동으로 간단한 재생목록으로 구성
- 상단 버튼이나 좌우 중앙의 얇은 손잡이로 최근 파일과 재생목록 열기
- `Ctrl + S`로 스크린샷 저장
- `Ctrl + 마우스 휠`로 영상을 확대하고, 가운데 버튼 드래그로 이동
- `I` → `O` → `Ctrl + E`로 간단한 무손실 구간 추출
- 전체화면 전용 버튼으로 조작 UI를 숨겨 영상만 표시
- `F1`로 여는 인앱 빠른 사용법과 전체 단축키 안내
- 환경설정에서 영어/한국어 표시 언어 선택
- 업데이트는 먼저 안내하고, 사용자가 선택할 때만 설치

## 🚀 3단계 설치

1. 최신 설치 파일을 받으세요:
   [DenoVideoPlayer-win-Setup.exe](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-win-Setup.exe)
2. 받은 `DenoVideoPlayer-win-Setup.exe`를 더블클릭해 설치하세요.
3. **Deno Video Player**를 열고 미디어 파일을 창에 끌어다 놓으세요.

압축 해제, 회원가입, 별도 코덱 설치는 필요 없습니다. 첫 실행 때 필요한 재생 도구를 자동으로 준비하므로 인터넷에 연결한 상태로 잠시 기다려 주세요.

아직 코드 서명되지 않은 앱이므로 Windows SmartScreen의 ‘인식할 수 없는 앱’ 경고가 나타날 수 있습니다. 공식 GitHub Releases에서 받은 파일인지 확인하고 신뢰하는 경우에만 **추가 정보 → 실행**을 선택하세요. 악성코드 탐지 등 다른 경고가 나오면 진행하지 말고 확인해 주세요. 보안 기능을 끌 필요는 없습니다.

### 시스템 요구사항

- Windows 10 또는 Windows 11, x64
- 첫 실행 때 mpv 재생 엔진을 준비하기 위한 인터넷 연결
- 첫 구간/오디오/비디오 내보내기 때 FFmpeg를 준비하기 위한 인터넷 연결

## ❓ 앱 안에서 보는 사용법

환경설정 옆의 `?` 버튼을 누르거나 언제든 `F1`을 누르세요. 도움말에서 다음 내용을 한 번에 확인할 수 있습니다.

- 최근 재생과 현재 폴더 재생목록의 위치
- 상단 버튼과 하단 재생 조작부의 기능
- 키보드와 마우스 단축키
- 자막·오디오 트랙, 스크린샷, 영상 확대와 이동
- 구간·오디오만·비디오만 저장하는 방법
- 첫 실행 및 재생 실패 문제 해결

빈 플레이어에는 **처음이신가요?** 링크가 있고, 재생 실패 화면에서는 문제 해결 항목으로 바로 이동할 수 있습니다.

## 📦 포터블 버전

설치 없이 쓰고 싶다면 [DenoVideoPlayer-v0.5.4-portable-win-x64.zip](https://github.com/Deno2026/deno-video-player/releases/download/v0.5.4/DenoVideoPlayer-v0.5.4-portable-win-x64.zip)을 받아 압축을 풀고 `DenoVideoPlayer.exe`를 실행하세요.

초보자에게는 `Setup.exe` 설치 버전을 추천합니다.

## 🎬 주요 기능

### 빠른 미디어 확인

영상, 오디오, 이미지, 자막이 있는 영상을 빠르게 열 수 있습니다. Deno Video Player는 무거운 라이브러리 관리보다 로컬 파일 확인에 집중합니다.

`F`, 화면·제목줄 더블클릭, 우측 상·하단의 크기 버튼은 모두 같은 동작입니다. 일반 창에서는 전체화면으로, 전체화면이나 Windows 최대화 상태에서는 이전의 일반 창 크기로 돌아옵니다. 빈 화면·로딩 중·재생 실패 상태에서도 같습니다. 파일을 열려면 **파일 열기** / **폴더 열기** 버튼, `Ctrl + O`, 끌어다 놓기를 사용하세요.

### 같은 폴더 탐색

파일 하나를 열면 같은 폴더의 주변 미디어를 간단한 재생목록처럼 사용할 수 있습니다. 렌더 결과, 다운로드 클립, 레퍼런스 파일을 확인할 때 편합니다.

상단 버튼 또는 `Ctrl + H`로 최근 파일을 열 수 있습니다. 현재 폴더 재생목록은 상단 버튼 또는 `P` / `Ctrl + L`로 엽니다. 이름순, 최신순, 오래된순 정렬을 선택하면 다음 실행에도 유지되며 이전/다음 이동 순서에도 함께 적용됩니다.

잠깐 확인하려면 왼쪽 가장자리 중앙의 얇은 손잡이에 마우스를 올려 최근 파일을, 오른쪽 손잡이로 재생목록을 여세요. 손잡이와 패널을 모두 벗어나면 닫힙니다. 상단 버튼이나 단축키로 연 패널은 직접 닫거나 다른 패널로 바꿀 때까지 유지됩니다.

하단 조작부에서는 반복 없음, 전체 반복, 한 항목 반복, 셔플, 음량, 배속, 전체화면을 조작합니다. 배속 값을 클릭하면 프리셋이 열리고, 그 위에서 마우스 휠을 돌리면 0.25x 단위로 바뀝니다.

### 재인코딩 없는 구간 추출

1. `I`로 시작점 지정
2. `O`로 끝점 지정
3. `Ctrl + E`로 저장

FFmpeg stream copy 방식이라 빠르고 화질 손실이 없습니다. 다만 키프레임 기준으로 저장되기 때문에 시작/끝 지점이 보이는 프레임과 조금 달라질 수 있습니다.

편집 모드에서는 전체 구간 저장, 오디오만 추출, 비디오만 추출 중에서 선택할 수 있습니다.
처음 내보낼 때 FFmpeg를 필요할 때만 준비하며, 다운로드 크기가 클 수 있습니다.

## 🧩 지원 파일

- **Video:** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **Audio:** `.mp3 .wav .flac .aac .m4a .mka .ogg .opus .wma .alac`
- **Image:** `.jpg .jpeg .png .webp .bmp .gif`
- **Subtitles:** `.srt .ass .ssa .vtt .sub .idx .sup .smi`

## 🌍 언어 지원

앱 내부 표시 언어는 현재 다음을 지원합니다.

- English
- 한국어

표시 언어는 **환경설정**에서 변경할 수 있습니다.

## ⌨️ 자주 쓰는 단축키

| 기능 | 단축키 |
| --- | --- |
| 재생 / 일시정지 | `Space` |
| 누르고 있는 동안 2배속 | `Space` 길게 누르기 |
| 5초 이동 | `←` / `→` |
| 30초 이동 | `Shift + ←` / `Shift + →` |
| 볼륨 | `↑` / `↓` 또는 마우스 휠 |
| 영상 확대 / 이동 | `Ctrl + 마우스 휠` / 가운데 버튼 드래그 |
| 음소거 | `M` |
| 전체화면 / 원래 창 크기 | `F` / `F11` / `Enter` / `Alt + Enter` / 화면·제목줄 더블클릭 |
| 전체화면 해제 | `Esc` |
| 이전 / 다음 파일 | `PageUp` / `PageDown` 또는 `Ctrl + ←` / `Ctrl + →` |
| 스크린샷 | `Ctrl + S` |
| 항상 위 | `Ctrl + T` |
| 최근 파일 | `Ctrl + H` |
| 재생목록 | `P` / `Ctrl + L` |
| 자막 트랙 | `V` / `Shift + V` |
| 오디오 트랙 | `Ctrl + J` |
| 구간 추출 | `I` → `O` → `Ctrl + E` |
| 사용 방법 및 단축키 | `F1` |

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
