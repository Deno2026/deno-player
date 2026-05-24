# Deno Player — Session Handoff

**Status (2026-05-24, v0.1.5):** 사용자가 자리 비운 동안 완성도 라운드 —
앱 아이콘, 단위 테스트, 로깅, self-contained publish, CI release pipeline.
**56/56 unit tests pass, dotnet build clean, smoke test 정상 기동/IPC 연결.**

## 이번 라운드(완성도) — 추가 내역

| 추가/수정 | 효과 |
|---|---|
| **앱 아이콘** (`tools/make-icon.ps1`로 생성한 `icon.ico` — 16/24/32/48/64/128/256 multi-res, DENO green play triangle on near-black) | 바탕화면 바로가기, 작업 표시줄, Alt-Tab, 작업 관리자 모두에서 DENO 브랜드 노출 |
| **WPF Window.Icon = pack URI** + csproj `<Resource Include="icon.ico"/>` | 윈도우 좌상단/타이틀바 아이콘도 동일 |
| **로깅 인프라** `Services/AppLog.cs` (1 MB 회전, 최대 2 파일) | `%APPDATA%\DenoPlayer\log.txt`에 startup·mpv pid·IPC 연결·예외 기록. 사용자가 문제 보고 시 추적 가능 |
| **단위 테스트** xunit 56 케이스 (자연 정렬, 확장자 분류, 시간 포맷, 폴더 스캔, 설정 JSON round-trip) | 회귀 즉시 잡힘. CI에서도 매번 실행 |
| `DenoPlayer.sln` 추가 | main + tests 한 솔루션. `dotnet test` 한 줄 |
| **CI release pipeline** (.github/workflows/build.yml) — main/PR마다 build+test, tag 푸시 시 framework-dependent + self-contained 둘 다 zip + GitHub Release 자동 생성 | tag 한 번에 사용자가 받을 수 있는 zip 두 가지 (런타임 포함 / 미포함) |
| **mpv 옵션 보강** `--cursor-autohide=no`, `--audio-display=no`, `--keep-open-pause=no`, `--input-cursor=yes` | 커서 관리는 우리가 (mpv와 충돌 X), 오디오 cover art 안 띄움, EOF 시 자동 next 보장 |
| `OpenFromExternal` 죽은 메서드 제거 (이미 `ReceiveExternalArgs`로 대체됨) | cleanup |
| README "진단" 섹션 (로그 위치 + 테스트 명령) | 사용자 문제 추적 안내 |

## 전체 어디까지 했나

| 영역 | 상태 |
|---|---|
| 프로젝트 스캐폴드 (.NET 8 WPF, net8.0-windows, x64) | ✅ |
| Solution 파일 (main + tests) | ✅ |
| `Themes/DesignTokens.xaml` 단일 디자인 출처 | ✅ |
| Services: Mpv 프로세스/IPC + Playlist + Settings + **SingleInstance** + **AppLog** | ✅ |
| Helpers: `Win32VideoHost`, 자연 정렬, 변환기, RelayCommand, TimeFormat | ✅ |
| ViewModel: state, commands, mpv property 양방향, MouseActivity, IsPlaylistOpen | ✅ |
| MainWindow: Grid 레이아웃 (Popup 제거), WindowChrome captionless, 풀스크린 | ✅ |
| 단축키, drag&drop, 명령줄 인자, single-instance, mpv crash 자동 복구 | ✅ |
| **앱 아이콘** (multi-res ico, WPF resource) | ✅ |
| **단위 테스트** 56/56 통과 | ✅ |
| **로깅** %APPDATA%\DenoPlayer\log.txt | ✅ |
| `tools/fetch-mpv.ps1` + `tools/install.ps1` + `tools/make-icon.ps1` | ✅ |
| README / LICENSE / NOTICE / .gitignore / .gitattributes | ✅ |
| **CI** build + test + release artifacts (fxdep + self-contained) | ✅ |
| `dotnet publish -c Release -r win-x64 --self-contained false` | ✅ `publish/DenoPlayer-win-x64/` |
| smoke test (app + mpv 정상 기동, IPC 연결, 로그 검증) | ✅ |
| **시각 동작** (영상 표시·풀스크린·자동 스크롤·아이콘) | ⏳ 사용자 손 테스트 |
| GitHub push | ⛔ 사용자 명시 승인 전 금지 |

## 알려진 trade-off (의도된 결정)

1. **컨트롤 영상 위 오버레이 없음** — Popup이 다른 앱 위에 떠 사용자 작업
   방해하던 문제. Grid 레이아웃으로 안정 우선. 풀스크린(`F`)에서는 자동 숨김.
2. **재생목록 hover 슬라이드 제거 → 명시적 토글** (`P` 키 / 우상단 📋)
3. **이미지 자동 다음 이동 없음** — `image-display-duration=inf`, 사용자가 PageDown
4. **Volume slider TwoWay** — 픽셀당 mpv 명령 발생. mpv 안정 처리

## 즉시 사용자가 할 일

기존 인스턴스는 죽여뒀습니다. 바탕화면 **"Deno Player"** 아이콘 다시
더블클릭 → 새 빌드(아이콘 + 로깅 + mpv 옵션 정리 적용).

체크 포인트 (이전 라운드와 동일 + 신규):
1. ✅ **첫 화면** NoFile 카드 + 상단 컨트롤 명확
2. ✅ **다른 앱으로 alt-tab** — Deno Player 컨트롤이 절대 다른 앱 위에 X
3. ✅ **두 번 더블클릭** — 두 번째는 첫 인스턴스에서 열림
4. ✅ **`P` 키 / 우상단 📋** — 재생목록 토글
5. ✅ **`F`** — 풀스크린 + 자동 숨김
6. 🆕 **바탕화면 아이콘** — 회색 .NET 아이콘이 아닌 검은 사각 + DENO 그린 ▶
7. 🆕 **작업 표시줄/Alt-Tab** — 같은 아이콘
8. 🆕 **`%APPDATA%\DenoPlayer\log.txt`** — 세션 트레이스가 시간 스탬프와 함께

## 잠재 버그/제약 (사용자 손 테스트 이후 추가 fix 여지)

| 항목 | 내용 |
|---|---|
| WindowChrome + 풀스크린 토글 | 한 프레임 깜빡임 가능 |
| 멀티 모니터 HiDPI | `PerMonitorV2` manifest 설정 — 모니터 이동 시 확인 |
| Glyph 누락 | Segoe Fluent Icons / MDL2 Assets 없으면 □ 표시 |
| Volume slider 변경 빈도 | mpv 명령 폭주 — 현재 영향 없으나 throttle 여지 |
| 이미지 모드 폴백 | mpv가 이미지 표시 실패 시 WPF Image fallback 없음 |
| 영상 위 컨트롤 오버레이 | 현재 없음 (Grid 레이아웃). owned-window 방식이 별도 라운드 |
| 시각 동작 | 사용자 손 테스트 결과 대기 |

## 다음 세션 첫 지시문 (사용자 결정 대기)

1. 사용자 손 테스트 → 시각 동작 피드백
2. 피드백 반영
3. GitHub repo 생성 + push (사용자 명시 승인 후)
   - 후보 이름: `deno-player`
   - `git tag v0.1.5` push 시 CI가 자동으로 두 zip(fxdep + self-contained) + GitHub Release 생성
4. Codex 메모리 ad_hoc 노트 반영 (사용자 승인 후)

— Claude Code (Opus 4.7)
