# Deno Player — Session Handoff

**Status (2026-05-26 KST):** PRIVATE repo. 자율 진행 ronde 종료, 사용자 피드백
대기 중. 65/65 unit tests pass, build 경고 0 / 오류 0. main HEAD `83a425b`.

## Repository

- **Repo**: https://github.com/Deno2026/deno-player (**PRIVATE**)
- **Release channel (별도 public repo, 미생성)**: `Deno2026/deno-player-releases`
  — 사용자가 만들 차례.
- **Tag**: 없음. 사용자 만족 확인 후 v0.4.0으로 첫 release.

## 이번 라운드 (자율 진행) 완료한 일

### 사용자 명시 요구
1. **3-mode repeat mutex 버튼 분리** (`1dd7cd6`) — 기존 cycle 1-button을 폐기.
   반복없음 / 전체반복 / 한곡반복 각각 독립 button + DataTrigger green
   active state. Shuffle button 별도 유지.
2. **Space 길게 = 2배속** — 처음 1초 timer였다가 사용자 피드백으로 YouTube
   동일 500ms로 조정 (`64834c9`).
3. **Slider 1:1 mapping** — anchor offset 없이 thumb 안 잡고 track 아무 위치
   click + drag로 즉시 조절 (`64834c9` + `ad70fdc` IsMoveToPointEnabled=False
   fix).
4. **환경설정 버튼 작동 안 함** — `SettingsWindow.xaml` 잘못된 StaticResource
   참조 제거 (`0821ba0`).
5. **SeekBar 중간 click + drag 안 됨** — Slider 내부 mouse capture 충돌 fix
   (`ad70fdc`).
6. **우측 hot zone 좌측처럼 상단 절반만** (`6533fb1`).
7. **스크린샷 폴더 사용자 지정** (`01baf7c`) — 환경설정 dialog picker +
   Settings.ScreenshotFolder.
8. **자동 업데이트 opt-in pull** (`ce04146` + `02fa1b3`) — Velopack 통합,
   TopBar 녹색 ☁⤓ 버튼 (click 시만 적용, 강제 X).

### 문서
- README.md 업데이트 (`b63fbcb`) — auto-update / 환경설정 / 스크린샷 폴더 /
  repeat·shuffle / settings.json 신규 필드 반영. "의도적으로 없는 것"에서
  `자동 업데이트` 제거.
- CHANGELOG.md 신규 (`83a425b`) — 이번 라운드 변경사항 정리, Velopack release
  notes 재료로 활용 가능.

## 설치 절차 (재정비, 변경 없음)

1. zip 압축 풀기
2. **`START_HERE.bat`** 더블클릭
3. (한 번만) mpv 자동 다운로드 → 바탕화면 바로가기 + 탐색기 우클릭 메뉴 등록

제거: **`UNINSTALL.bat`** 더블클릭.

## 현재 main HEAD (unreleased) 최근 commit

```
83a425b CHANGELOG: 2026-05-26 빌드 정리
b63fbcb README: 새 기능 반영
6533fb1 우측 hover panel 세로 상단 절반만 trigger
ad70fdc Slider drag fix (IsMoveToPointEnabled=False)
0821ba0 환경설정 안 열리던 버그 fix (XAML 잘못된 StaticResource)
64834c9 Space hold 0.5초 (YouTube) + Slider 1:1 mapping
1dd7cd6 Repeat 3-mode mutex 버튼 분리
30c0619 Slider track click+drag (SeekBar / Volume)
01baf7c Screenshot 폴더 사용자 지정
02fa1b3 Auto-update opt-in pull
ce04146 Auto-update Velopack framework
1bd1bb3 PlaylistList IsVirtualizing=False
```

## Pending (사용자 측)

1. **자동 업데이트 채널 활성화** — 별도 public repo
   `Deno2026/deno-player-releases` 생성 + `dotnet tool install --global vpk`
   + `pwsh tools\pack-velopack.ps1`로 release 만들기.
2. **첫 release tag** (v0.4.0 추천) — 사용자가 실제로 써본 후 결정.

## 알려진 잠재 이슈 (다음 사이클 시 참고)

- **PlaylistWindow visual render 회귀 (이전 라운드)**: GetCursorPos polling으로
  `ShowSlide()` 정상 호출 + position 정상 trace 확인. screenshot에 contents가
  안 보였던 건 dev 환경 (DPI / screenshot capture artifact) 의심. 사용자가 실제
  사용 시 확인 필요. IsVirtualizing=False는 들어가 있음.

## 자율 진행 가드 (다음 세션이 참고할 것)

- push는 사용자가 명시 승인했음 — **private origin/main에 한해** 자유 push
  OK.
- Velopack publish (public release repo로) / tag bump / public 노출은 **사용자
  명시 승인 후만**.
- 사소한 코드 결정·진행 여부는 자율 (`.codex\memories\extensions\ad_hoc\notes\`
  와 CLAUDE.md "자율 진행" 룰).

## CI

- main / PR push마다 `build` job (test 포함).
- `v*` tag push 시 `release` job — 현재 tag 없음, 사용자 결정 후 진행.

— Claude Code (Opus 4.7)
