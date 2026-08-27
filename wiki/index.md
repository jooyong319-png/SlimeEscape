# index — 페이지 카탈로그

> 모든 페이지 + 한 줄 요약. 새 페이지 만들면 여기 추가.
> 처음이면 [[SCHEMA]]부터.

**SlimeEscape** = **한 걸음 옮기면 몸이 한 겹 줄고, 먹이를 덮으면 느는 격자 퍼즐.**

- `d:/Make_game/SlimeEscape` · Unity 6000.3.22f1 · itch.io 웹 · 시작 2026-08-27
- 🔴 조작은 **← →** 둘뿐. 위아래는 중력과 몸집이 정한다
- 🔴 **크기 하나가 세 가지를 정한다** — 얼마나 더 가나 · 어디 들어가나 · 얼마나 오르나
- 🔴 **낙하는 공짜**, 오르는 건 몸집이 든다. 그래서 지형이 곧 문제지다
- 🔴 **지는 게 없다.** 갇히면 되돌리거나 다시 하면 된다

## 지금 어디까지 왔나 (2026-08-27)

**1단계(킬 테스트) 통과 → 2단계(수직 슬라이스) 진행 중.**

- HTML 시안 rev.2로 사장님이 플레이 → *"재밌을 것 같아, 유니티로 구현하고 좀 다듬기만 해도"*
- Unity 프로젝트 생성 · 규칙 C# 포팅 · 씬 코드 생성 · 컴파일 통과
- 적합성 검사 통과 — C# 엔진이 JS 솔버와 같은 답을 낸다
- ⚠️ **아직 아무도 유니티 화면을 안 봤다.** 컴파일 통과 ≠ 동작

## 🔴 지금 가장 중요한 한 줄

**판 6개 중 5개가 "오른쪽만 누르면" 풀린다.** 규칙은 서는데 판이 아직 결정을 안 만든다.
이게 `docs/project-brief.md` §5의 가장 위험한 가정이고, [[todo]] 맨 위다.

## 문서

| 어디 | 무엇 |
|---|---|
| `docs/project-brief.md` | 🔴 **기준 문서.** 핵심 루프 · 설계 원칙 · MVP · 안 할 것 목록 · 게이트 |
| `docs/success-state.md` | 잘 됐을 때 30분이 어떤 모습인가 |
| `docs/distribution.md` | itch.io 경로 · GIF가 왜 MVP 필수인가 |
| `README.md` | 셋업 · 도구 사용법 · 알려진 함정 |

## 위키

| 문서 | 무엇 |
|---|---|
| [[SCHEMA]] | 위키 사용법 + 네 원칙 (먼저 읽을 것) |
| [[design-lineage]] | 🔴 **왜 이 모양이 됐는지.** rev.1 세로 성장 → rev.2 중력·정사각 |
| [[todo]] | 🔴 **남은 일과 사장님이 정하실 것.** 맨 위가 최신 |
| [[playtests]] | 사람이 실제로 해보고 한 말 |

## 코드 지도

| 무엇 | 어디 |
|---|---|
| 🔴 **규칙 정본 (JS)** | `tools/engine.js` |
| 규칙 사본 (C#) | `game/Assets/Scripts/SlimeEngine.cs` — 적합성 검사로 묶여 있다 |
| 🔴 **판 데이터 정본** | `game/Assets/Resources/levels.json` — 사본 없음. 도구도 이 파일을 읽고 쓴다 |
| 판 검증·표기값 박기 | `tools/stamp.js` |
| 먹이 배치 탐색 | `tools/search.js` |
| 수순 재생 (판 그려서 확인) | `tools/play.js` |
| HTML 시안 rev.2 | `tools/proto-rev2.html` |
| 되살린 도트 + 팔레트 | `art/` |
| 화면·입력 | `game/Assets/Scripts/GameController.cs` |
| 씬 생성 · 검사 | `game/Assets/Editor/` |

## 태그

#slime #puzzle #unity #pixelart #wiki
