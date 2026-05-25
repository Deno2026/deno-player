# Deno Player — Session Handoff

**Status (2026-05-25, v0.3.0):** 사용자 자율 진행 라운드 종료 상태.
v0.1.5 이후 사용자 손 테스트 피드백을 받아가며 hover 패널 2개(우측 재생목록 /
좌측 최근), Repeat/Shuffle 라이브 컨트롤, 핫존 정밀화, mpv 백엔드 고정, seek
드래그/클릭/스냅백 해결, 파일 연결 4-tier 등록, Space hold 2x, Enter
전체화면, 엣지 힌트 스트립까지 누적. **57/57 unit tests pass, dotnet build
clean (warning 0 / error 0), 작업 트리 clean. push 대기.**

## 이번 라운드(자율 진행, 48 commit) — 핵심 변경 요약

### UX / 패널

| 영역 | 변경 |
|---|---|
| **재생목록 hover (우측)** | Popup 폐기 → owned `PlaylistWindow` (별도 child Window). 다른 앱 위로 떠다니는 문제 해결. 핫존 180 px, **하단 재생바 영역은 트리거에서 제외**(BottomBar 가리지 않음). 같은 폴더에서 다른 곡 골라도 패널 그대로 둠. |
| **최근 재생 hover (좌측)** | 신규 `RecentWindow` 추가, **상단 절반에서만** 트리거(영상 조작 방해 최소화), 클릭 즉시 HideSlide. |
| **엣지 힌트 스트립** | 좌·우 가장자리에 3 px LinearGradient 가이드 — 패널 존재 시각화. 더블클릭(전체화면) 영역에서 4 px 예외 처리해 토글 오발 방지. |
| **Repeat / Shuffle** | 하단 재생바에 토글 버튼, 호버 시 즉시 툴팁 (RepeatLabel/ShuffleLabel binding), MouseLeave 시 강제 닫힘. |
| **Settings 다이얼로그** | 환경설정 → 파일 연결 등록 UI (실제 ProgID / Capabilities / RegisteredApplications 4-tier 등록). |
| **Space hold** | 단발 = play/pause, 꾹 = 2x 재생 (YouTube 식). focus 잃을 때 자동 복원(`Deactivated` 핸들러). hold 시작에 OSD 표시 + 타이머 reset. |
| **Enter / Return** | 전체화면 토글 키 추가 (NumPad Enter 포함). |

### 재생 안정성

| 영역 | 변경 |
|---|---|
| **mpv EOF 침묵 크래시 수정** | `--keep-open=yes`가 `--wid` 모드에서 silent exit-1 → `--keep-open=no` + `--idle=yes`로 mpv 유지, end-file 이벤트 정상 발화. |
| **mpv 백엔드 고정** | `--vo=gpu --gpu-api=d3d11`, `--video-sync=audio`, `--hwdec=auto-safe` — 패널 슬라이드 중 영상 비율 흔들림 제거. |
| **Repeat OFF 실 반영** | fire-and-forget 명령 누락 → `CommandAsync` + `get_property` readback verify로 `loop-file` 실제 동기화. PlayMedia / OnIpcConnected에서도 sync. |
| **same-kind 자동 재생** | 동영상 끝 → png 자동 점프 버그 → 시드 파일의 MediaKind만 픽업하는 `NextOfSameKind` / `FirstOfSameKind` / `PickRandomOfSameKind`. |
| **Seek 정상화** | 클릭 시 자리에 안 가던 버그 → Slider.Value 강제 + EndSeek 단일 경로 + DragCompleted no-op + 250 ms Seeking 플래그 + mouse-X ratio fallback. |
| **Playlist position in fullscreen** | WPF Left/ActualWidth 못 믿음 → Win32 `GetWindowRect` + DPI scale로 owned 패널 좌표 산출. |

### 인프라

| 영역 | 변경 |
|---|---|
| **로그 stderr 수집** | mpv 표준 에러를 `mpv!` prefix로 AppLog에 합류. |
| **파일 연결 4-tier** | Applications + ProgID(`DenoPlayer.Media`) + Capabilities + RegisteredApplications. PowerShell 5.1 codepage 호환 ASCII-only registry value. |
| **단위 테스트** | 56 → 57 (loop-file readback + same-kind playlist regression). |
| **XAML PUA glyph 손상** | BAML에 inline PUA character 들어가면 IndexOutOfRange — 모든 아이콘을 `&#xE921;` XML 엔티티로 교체. |
| **버전 표기** | csproj `0.1.0` → `0.3.0`. |

## 전체 상태

| 영역 | 상태 |
|---|---|
| 프로젝트 스캐폴드 (.NET 8 WPF, net8.0-windows, x64) | ✅ |
| Solution (main + tests), `dotnet test` 한 줄 | ✅ |
| `Themes/DesignTokens.xaml` 단일 디자인 출처 | ✅ |
| Services: Mpv 프로세스/IPC + Playlist + Settings + SingleInstance + AppLog | ✅ |
| Helpers: `Win32VideoHost`, 자연 정렬, 변환기, RelayCommand, TimeFormat | ✅ |
| ViewModel: state, commands, mpv 양방향, MouseActivity, IsPlaylistOpen, Repeat/Shuffle | ✅ |
| MainWindow: WindowChrome captionless, 풀스크린, hot zone 분리, 엣지 힌트 | ✅ |
| **PlaylistWindow / RecentWindow** owned child Window (Popup 폐기) | ✅ |
| **Settings 다이얼로그 + FileAssociationService 4-tier 등록** | ✅ |
| 단축키, drag&drop, 명령줄 인자, single-instance, mpv crash 자동 복구 | ✅ |
| 앱 아이콘 (multi-res ico, WPF resource, 작업표시줄/Alt-Tab/타이틀바 일관) | ✅ |
| 로깅 `%APPDATA%\DenoPlayer\log.txt` (1 MB 회전 / mpv stderr 합류) | ✅ |
| 단위 테스트 **57/57 통과** | ✅ |
| `tools/fetch-mpv.ps1` + `tools/install.ps1` + `tools/make-icon.ps1` | ✅ |
| README / LICENSE / NOTICE / .gitignore / .gitattributes | ✅ |
| CI build + test + release artifacts (fxdep + self-contained) | ✅ |
| `dotnet publish -c Release -r win-x64 --self-contained false` | ✅ `publish/DenoPlayer-win-x64/` |
| **시각 동작** (사용자 손 테스트) — 48 commit 누적 피드백 반영 | ✅ |
| GitHub repo 생성 + push | ⛔ 사용자 명시 승인 전 금지 |

## 알려진 trade-off (의도된 결정)

1. **재생목록 패널은 owned child Window** — Popup이 다른 앱 위로 떠 사용자
   작업 방해하던 문제. 영상 위 오버레이가 아니라 영상은 그대로, 패널이
   별도 윈도로 owner에 묶여 z-order/최소화/닫힘 동기화.
2. **이미지 자동 다음 이동 없음** — `image-display-duration=inf`, 사용자가 PageDown.
3. **마지막 재생 위치 저장 안 함** — 의도. 세션 내 자동 진행만.
4. **자동 업데이트 / 설치 프로그램 / 코덱팩 권유 / 분석 / 클라우드 — 전부 없음.**
5. **단축키 설정 UI 없음** — 기본 관습 키 고정.
6. **검색창 / 썸네일 / 태그 / 라이브러리 DB 없음** — 의도된 미니멀.

## 사용자 결정 대기

1. **시각 동작 추가 피드백** — 사용자가 v0.3.0 직접 써본 후
2. **GitHub repo 생성 + push** — 사용자 명시 승인 후
   - 후보 이름: `deno-player` (현 로컬 디렉터리명과 동일)
   - 첫 push: `git push -u origin main`
   - 릴리스: `git tag v0.3.0 && git push origin v0.3.0` →
     CI가 자동으로 fxdep(~3 MB) + self-contained(~150 MB) zip + GitHub Release
3. **Codex 메모리 ad_hoc 노트 반영** — 사용자 승인 후

## 잠재 follow-up (사용자 손 테스트 이후 여지)

| 항목 | 내용 |
|---|---|
| WindowChrome + 풀스크린 토글 | 한 프레임 깜빡임 가능 (현재 무방함) |
| 멀티 모니터 HiDPI | `PerMonitorV2` manifest 적용 — 모니터 이동 시 추가 검증 |
| Glyph 누락 | Segoe Fluent Icons / MDL2 Assets 없으면 □ — Win 10 일부 환경 |
| Volume slider 변경 빈도 | mpv 명령 폭주 가능성 — 현재 영향 없으나 throttle 여지 |
| 이미지 모드 폴백 | mpv가 이미지 표시 실패 시 WPF Image fallback 없음 |

— Claude Code (Opus 4.7)
