# Deno Player

윈도우에서 가볍게 실행되는 **로컬 미디어 셸 플레이어**. 영상/오디오/이미지 파일을
탐색기에서 빠르게 열어 확인하는 게 전부다. 광고, 계정, 클라우드, 추천, 스토어,
플러그인, 라이브러리 색인 — 전부 없음.

```
 ┌──────────────────────────────────────────────────────────┐
 │● clip.mp4                            📌 📷 🗀 — □ ✕     │
 │                                                          │
 │                                                          │
 │                    [ 영상 영역 ]                          │
 │                                                          │
 │                                                          │
 │ 00:12 ━━━━━●─────────────────────────────────────── 04:58│
 │   ⏮  ▶  ⏭             🔇━●━━━━  1.0x   ⛶                │
 └──────────────────────────────────────────────────────────┘
                                       │  hover →  재생목록 │
```

## 설계 요약

- **C# .NET 8 + WPF**. 단일 EXE. Electron/webview 없음.
- **mpv를 별도 프로세스로 띄우고 named pipe JSON IPC로 제어**.
- 영상은 child Win32 window(`--wid`)에 attach.
- 코덱/디코더는 mpv에 위임. 자체 디코더 없음.
- 컨트롤 오버레이는 마우스가 움직일 때만 잠깐 보이고 곧 사라진다.
- 같은 폴더의 지원 미디어를 자연 정렬로 즉석 재생목록.
- 네트워크/계정/텔레메트리 코드 0줄.

### 패널 / 보조 컨트롤

- **우측 가장자리 hover** → 같은 폴더 재생목록 패널 슬라이드 (180 px 핫존,
  하단 재생바 위만 트리거. 패널 안에서 곡 클릭 = 즉시 재생).
- **좌측 가장자리 hover, 상단 절반만** → 최근 재생 파일 패널 슬라이드
  (160 px 핫존. 재생 중 다른 영상 보고 싶을 때 흐름 끊지 않고 호출).
- **반복 / 셔플** 버튼 (하단 재생바) — 전체반복 ↔ 한 곡 반복 ↔ 끔, 셔플 on/off.
  현재 상태는 호버 툴팁으로 즉시 확인.
- **Space 꾹 누르기** → 누르고 있는 동안 2배속 재생, 손 떼면 원래 속도.
- **드래그 앤 드롭** — 영상/오디오/이미지를 창에 놓으면 즉시 재생 + 해당
  폴더 자동 재생목록 재생성. **자막 파일(.srt .ass .vtt 등)을 같이 또는
  단독으로 드롭**하면 현재 영상에 add-sub.
- **재생목록 / 최근 패널에서 우클릭** → "탐색기에서 열기" / "파일 경로 복사" /
  "이 항목 제거"(최근만).

## 받자마자 쓰기 (탐색기 통합)

이미 빌드된 상태라면 — **PowerShell 한 줄이면 끝**.

```pwsh
pwsh -ExecutionPolicy Bypass -File .\tools\install.ps1
```

스크립트가 자동으로 처리해주는 것:
1. **바탕화면 바로가기** "Deno Player" 생성
2. **시작 메뉴**에 "Deno Player" 등록
3. **탐색기 우클릭 → 연결 프로그램**에 "Deno Player" 노출 (모든 지원 확장자)

이제 사용법:
- **바탕화면 아이콘 더블클릭** → 빈 상태로 실행 → 영상 끌어다 놓기
- **탐색기에서 영상 우클릭 → 연결 프로그램 → Deno Player** → 즉시 재생 + 같은 폴더 자동 재생목록
- **기본 앱으로 등록**하려면 우클릭 메뉴의 "다른 앱 선택" → "Deno Player" → "항상 이 앱으로 열기" 체크

지우려면:
```pwsh
pwsh -ExecutionPolicy Bypass -File .\tools\install.ps1 -Uninstall
```

## 빌드 (소스에서 시작할 때)

```pwsh
# 1) .NET 8 SDK가 있어야 한다 (winget으로 설치 가능)
winget install Microsoft.DotNet.SDK.8

# 2) mpv.exe 가져오기 (한 번만)
pwsh -ExecutionPolicy Bypass -File .\tools\fetch-mpv.ps1

# 3) 빌드
dotnet build -c Release

# 4) 배포 폴더 만들기
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish\DenoPlayer-win-x64

# 5) 탐색기 통합
pwsh -ExecutionPolicy Bypass -File .\tools\install.ps1
```

`publish\DenoPlayer-win-x64\` 폴더를 그대로 다른 PC에 복사해서 거기서
`tools\install.ps1` 만 실행하면 그 PC에서도 똑같이 쓸 수 있다. mpv.exe는
라이선스 분리 정책으로 같이 묶지 않으니, 새 PC에서도 `fetch-mpv.ps1`을
한 번 더 돌리면 된다.

## mpv 설치

Deno Player는 **mpv.exe를 같이 배포하지 않는다** (GPL/LGPL 라이선스 분리).
다음 중 하나로 받는다.

### A. 자동 (권장)

```pwsh
pwsh -ExecutionPolicy Bypass -File .\tools\fetch-mpv.ps1
```

스크립트는 [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild)
최신 릴리스의 x86_64 빌드를 받아 `runtime\mpv\mpv.exe`에 둔다. 7-Zip이
설치돼 있어야 자동 압축 해제까지 끝난다 (`winget install 7zip.7zip`).

### B. 수동

1. https://mpv.io/installation/ 의 Windows 빌드 (`shinchiro` / `zhongfly`
   등) 중 64-bit 정적 빌드를 받는다.
2. 압축 해제 후 `mpv.exe`(+ 보조 DLL이 있다면)를 다음 경로에 둔다:
   ```
   <repo or install dir>\runtime\mpv\mpv.exe
   ```
3. Deno Player를 실행한다. `mpv.exe`가 없으면 시작 화면에 안내가 표시된다.

## 단축키

| 키 | 동작 |
|---|---|
| `Space` | 재생/일시정지 |
| `Space` 길게 누르기 | 누르고 있는 동안 2배속 (YouTube 식) |
| `←` / `→` | 5초 뒤로/앞으로 |
| `Shift + ← / →` | 30초 뒤로/앞으로 |
| `↑` / `↓` | 볼륨 5% |
| `M` | 음소거 |
| `F` / `F11` / `Enter` / `Alt+Enter` | 전체화면 토글 |
| `Esc` | 전체화면 해제 |
| `PageUp` / `PageDown` | 이전/다음 파일 |
| `Shift + . / ,` | 배속 +0.25 / -0.25 |
| `.` / `,` | 프레임 step / back step |
| `Ctrl + O` | 파일 열기 |
| `Ctrl + S` | 스크린샷 (Pictures\DenoPlayer\) |
| `Ctrl + T` | 항상 위 토글 |
| `P` / `Ctrl + L` | 재생목록 패널 토글 |
| `V` | 자막 트랙 사이클 (다중 자막 영상) |
| `Shift + V` | 자막 표시 켬/끔 |
| `Ctrl + J` | 오디오 트랙 사이클 (다국어 영상) |
| 더블클릭 | 전체화면 토글 |
| 마우스 휠 | 볼륨 |
| 우측 가장자리 hover (180px, 하단 재생바 제외) | 재생목록 패널 슬라이드 |
| 좌측 가장자리 hover (160px, **상단 절반만**) | 최근 재생 패널 슬라이드 |

## 지원 파일

- **비디오** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **오디오** `.mp3 .wav .flac .aac .m4a .ogg .opus .wma .alac`
- **이미지** `.jpg .jpeg .png .webp .bmp .gif` (mpv가 표시)

## 진단

- **로그**: `%APPDATA%\DenoPlayer\log.txt` (1 MB 회전, 최대 2 파일).
  세션 시작·mpv pid·IPC 연결·예외가 시간 스탬프와 함께 들어간다.
  뭔가 이상하면 이 파일과 함께 알려주면 추적이 빠르다.
- **단위 테스트**:
  ```pwsh
  dotnet test tests\DenoPlayer.Tests\DenoPlayer.Tests.csproj -c Release
  ```
  자연 정렬, 확장자 분류, 시간 포맷, 폴더 스캔, 설정 직렬화 회귀 방지.

## 설정 파일

`%APPDATA%\DenoPlayer\settings.json` — 단일 파일. 설정 UI는 없다.
저장 값:

- `windowWidth/Height`, `windowLeft/Top`, `windowMaximized`
- `volume`, `muted`, `playbackRate`
- `lastOpenedFolder`
- `autoPlayNext`
- `controlAutoHideMs` (기본 2500)
- `playlistPanelEnabled` (기본 true)
- `alwaysOnTop`

마지막 재생 위치는 저장하지 않는다 (재생목록 자동 진행만 유지).

## 명령줄

```
DenoPlayer.exe                       # 빈 상태로 실행
DenoPlayer.exe "C:\path\video.mp4"   # 파일 즉시 열기 + 같은 폴더 재생목록
```

탐색기에서 "연결 프로그램"으로 등록하면 더블클릭 한 번에 열린다 (자동 등록 UI는
없음 — Windows의 기본 앱 설정에서 직접 지정).

## 의도적으로 없는 것

광고 · 계정 · 로그인 · 클라우드 · 텔레메트리 · 분석 · 추천 · 뉴스 · 스토어 ·
스킨 마켓 · 플러그인 시스템 · 코덱팩 권유 · 자동 업데이트 · 영상 변환 · 영상 편집 ·
캡처/녹화(스크린샷 외) · AI 기능 · 자막 싱크 편집기 · 미디어 라이브러리 색인 ·
폴더 전체 DB 스캔.

## 구조

```
DenoPlayer.csproj
App.xaml / App.xaml.cs                — 진입점, 명령줄 인자 처리
MainWindow.xaml / .xaml.cs            — 셸 화면, 컨트롤 자동 숨김, 풀스크린
Themes/DesignTokens.xaml              — 색·간격·시간 단일 출처(디자인 DNA)
Models/MediaKind.cs                   — 확장자 분류 단일 출처
Models/MediaItem.cs                   — 재생목록 항목
Models/AppSettings.cs                 — 설정 모델
Services/MpvProcessService.cs         — mpv.exe 프로세스 관리(시작/종료/crash)
Services/MpvIpcClient.cs              — Windows named pipe JSON IPC 클라이언트
Services/PlaylistService.cs           — 폴더 스캔 + 자연 정렬
Services/SettingsService.cs           — %APPDATA%\DenoPlayer\settings.json
ViewModels/MainViewModel.cs           — 상태/명령
Helpers/                              — Win32 child window, RelayCommand, 자연정렬, …
runtime/mpv/mpv.exe                   — 사용자가 직접 배치
tools/fetch-mpv.ps1                   — mpv 자동 다운로드
```

## 라이선스

코드 본체는 **MIT**. 자세한 건 [LICENSE](LICENSE) / [NOTICE.md](NOTICE.md).
mpv 바이너리는 별도 GPLv2+/LGPLv2.1+, 사용자가 직접 받아 사용.
