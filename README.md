# Deno Player

윈도우에서 가볍게 실행되는 **로컬 미디어 셸 플레이어**. 영상/오디오/이미지를
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

---

## 🚀 설치 (3단계, 끝)

1. **zip 압축 풀기**
2. **`START_HERE.bat`** 더블클릭
3. (한 번만) mpv 자동 다운로드 + 바탕화면 아이콘·탐색기 우클릭 메뉴 등록 — 끝.

이후 사용법:

- 🖱 **바탕화면 "Deno Player" 더블클릭** → 빈 상태로 실행 → 영상 끌어다 놓기
- 🖱 **탐색기에서 영상 우클릭 → 연결 프로그램 → Deno Player** → 즉시 재생
- 기본 앱으로 등록하려면 "다른 앱 선택" → "Deno Player" → "항상 이 앱으로 열기" 체크

제거하려면 **`UNINSTALL.bat`** 더블클릭 (폴더 자체는 삭제하셔도 됩니다).

---

## 🔄 자동 업데이트

새 버전이 publish되면 **다음 실행 시** Deno Player가 자동으로 확인합니다.
새 버전이 있으면 좌상단 도구 막대 가장 왼쪽에 **녹색 업데이트 아이콘** 이
나타납니다. **클릭 = 다운로드 + 적용 + 재시작**. 클릭 안 하면 그대로 작동
(강제 안 함, pull 방식).

기본 update channel: `github.com/Deno2026/deno-player-releases` 별도 public
repo. 다른 host를 쓰려면 환경변수 `DENO_PLAYER_UPDATE_URL`을 설정하세요.

---

## 단축키

| 키 | 동작 |
|---|---|
| `Space` | 재생/일시정지 |
| `Space` 길게 누르기 | 2배속 (YouTube 식) |
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
| 우측 가장자리 hover | 같은 폴더 재생목록 슬라이드 |
| 좌측 가장자리 hover (상단 절반) | 최근 재생 슬라이드 |
| 재생목록/최근 패널 우클릭 | 탐색기에서 열기 · 경로 복사 · 항목 제거 |

## 지원 파일

- **비디오** `.mp4 .mkv .mov .webm .avi .m4v .ts .mts .m2ts .wmv .flv .3gp`
- **오디오** `.mp3 .wav .flac .aac .m4a .ogg .opus .wma .alac`
- **이미지** `.jpg .jpeg .png .webp .bmp .gif`
- **자막** `.srt .ass .ssa .vtt .sub .idx .sup .smi` (드래그앤드롭으로 add-sub)

---

## ⚙️ 고급: 소스에서 빌드

```pwsh
# 1) .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# 2) build
dotnet build -c Release

# 3) publish (배포 폴더 만들기)
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish\DenoPlayer-win-x64

# 4) 그 폴더의 START_HERE.bat 더블클릭
```

## ⚙️ 고급: 진단

- **로그**: `%APPDATA%\DenoPlayer\log.txt` (1 MB 회전, 최대 2 파일).
  세션 시작·mpv pid·IPC 연결·예외가 시간 스탬프와 함께 들어간다.
- **단위 테스트**: `dotnet test tests\DenoPlayer.Tests\DenoPlayer.Tests.csproj -c Release`

## ⚙️ 고급: 설정 파일

`%APPDATA%\DenoPlayer\settings.json` — 단일 파일, UI 없음. 저장 값:
- `windowWidth/Height`, `windowLeft/Top`, `windowMaximized`
- `volume`, `muted`, `playbackRate`
- `lastOpenedFolder`, `autoPlayNext`
- `controlAutoHideMs` (기본 2500)
- `playlistPanelEnabled`, `alwaysOnTop`
- `recentFiles` (최근 30개)

마지막 재생 위치는 저장하지 않는다 (재생목록 자동 진행만 유지).

## 명령줄

```
DenoPlayer.exe                       # 빈 상태로 실행
DenoPlayer.exe "C:\path\video.mp4"   # 파일 즉시 열기 + 같은 폴더 재생목록
```

탐색기에서 "연결 프로그램"으로 등록하면 더블클릭 한 번에 열린다.

## 의도적으로 없는 것

광고 · 계정 · 로그인 · 클라우드 · 텔레메트리 · 분석 · 추천 · 뉴스 · 스토어 ·
스킨 마켓 · 플러그인 시스템 · 코덱팩 권유 · 자동 업데이트 · 영상 변환 · 영상 편집 ·
캡처/녹화(스크린샷 외) · AI 기능 · 자막 싱크 편집기 · 미디어 라이브러리 색인 ·
폴더 전체 DB 스캔.

## 라이선스

코드 본체는 **MIT**. 자세한 건 [LICENSE](LICENSE) / [NOTICE.md](NOTICE.md).
- `mpv.exe` 바이너리는 별도 GPLv2+/LGPLv2.1+, `START_HERE.bat`가 [zhongfly/mpv-winbuild](https://github.com/zhongfly/mpv-winbuild) 빌드를 자동으로 받음.
- `tools/7zr.exe`는 7-Zip 프로젝트(LGPL)의 standalone command-line tool, free redistribution.
