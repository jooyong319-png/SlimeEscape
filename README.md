# SlimeEscape

**몸이 사슬처럼 길어지는 격자 퍼즐.** 유적을 내려가며 문에 파인 홈을 제 몸으로 정확히 채워 연다.
Unity 6000.3.22f1 · itch.io 웹 · 시작 2026-08-27 · **rev.4 (2026-08-28)**

## 🔴 판단이 갈리면

**`docs/project-brief.md`로 결정한다.** 설계 근거는 `docs/design-study.html`(더블클릭하면 열림).
맥락은 `wiki/` — 처음이면 `wiki/SCHEMA.md` → `wiki/index.md` 순으로.

## 규칙 (전부)

1. 머리를 **상하좌우 한 칸**. 몸이 따라온다. **중력 없음**
2. 조각을 먹으면 **길이 +1**. 줄어드는 건 없다
3. 벽·자기 몸에 부딪히는 걸음은 **막힌다**. 죽지 않는다 — 못 갈 뿐
4. 🔴 **표시된 칸을 몸으로 정확히 채우면** 문이 열린다. 남아도 모자라도 안 된다
5. **심(心)** — 머리가 마지막에 있어야 할 칸
6. 되돌리기 무제한 (`Z`) · 재시작 (`R`)

→ 길이 = 목표 칸 수. 그래서 **조각은 목표 칸 수 − 1개**다.

## 판 기호

`#` 벽 · `.` 빈칸 · `S` 시작 · `+` 조각 · `=` 채워야 할 칸 · `*` 심

## 설계 원칙 ↔ 코드 위치

| 원칙 | 어디에 강제돼 있나 |
|---|---|
| 규칙 정본은 하나 | `game/Assets/Scripts/SnakeEngine.cs` |
| 길이가 안 맞는 판은 못 만든다 | `SnakeEngine.Parse`가 예외를 던진다 |
| 씬에 손으로 배치한 것 없음 | `Assets/Editor/ProjectBootstrap.cs`가 코드로 만든다 |
| 🔴 연출이 칸을 안 속인다 | `SnakeController.Animate` — 끌림은 **시간차**로만 |

## 도구

```bash
node tools/sizes.js            # 판마다 몸이 몇까지 커지는지 (화면을 꽉 채우면 답답하다)
node tools/stamp.js            # 판 검증만 (안 풀리거나 표기값 어긋나면 실패)
node tools/stamp.js --write    # 검증 + best/sol/back 갱신
node tools/play.js <판목록.json> <id> <시작크기>   # 수순을 판으로 재생해서 눈으로 확인
node tools/search.js <뼈대.json>                   # '?' 칸의 먹이 배치를 기계가 찾는다
```

`tools/proto-rev2.html` — 브라우저에서 바로 도는 시안. 규칙을 빨리 시험할 때 여기가 제일 싸다.

## Unity 검사 (빌드 아님)

```bash
U="C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe"
"$U" -batchmode -quit -nographics -projectPath game -executeMethod SlimeEscape.EditorTools.ConformanceCheck.Run
"$U" -batchmode -quit -nographics -projectPath game -executeMethod SlimeEscape.EditorTools.AssetCheck.Run
"$U" -batchmode -quit -nographics -projectPath game -executeMethod SlimeEscape.EditorTools.ProjectBootstrap.BuildFromCli
```

셋 다 실패하면 종료코드가 0이 아니다. 로그는 `tools/unity-*.log`.

## ⚠️ 손대기 전에 알아야 할 것

- **규칙은 두 벌 있다** (`tools/engine.js`, `Assets/Scripts/SlimeEngine.cs`).
  한쪽만 고치면 적합성 검사가 깨진다. **양쪽을 같이 고칠 것**
- **판당 먹이·불은 30개까지** — 비트마스크 한계. 양쪽 엔진 공통
- `art/`의 도트는 **GIF에서 되살린 원본**이다. 색은 `art/PALETTE.md`에 있다.
  새로 그릴 땐 그 색을 그대로 쓸 것
- **원본 GIF는 `d:/Make_game/*.gif`에 있다.** 작업 파일이 유실돼 그게 마지막 원본이다 — **지우지 말 것**
- HUD가 `OnGUI`다. **임시다** — 4단계에서 교체. 이전 프로젝트가 이걸 끝까지 끌고 갔다

## 알려진 함정 (실제로 겪은 것, 날짜와 함께)

- **2026-08-27** 검사가 엉뚱한 걸 재고 있어도 초록불은 켜진다.
  *"최단해가 유일한 판만 골랐다"*가 통과했는데 **고를 게 없어서** 유일한 거였다 → `back` 지표를 추가
- **2026-08-27** 몸집을 세로→정사각으로 바꾸니 **불값 −3이 감당 불가**가 됐다.
  덩어리가 크면 멀리서도 닿는다. **몸집이 바뀌면 같은 숫자가 다른 뜻이 된다**
- **2026-08-28** **몸 크기를 안 재고 있었다.** 판이 다 통과했는데 슬라임이 화면을 꽉 채웠다 —
  불 판은 열린 줄이 5인데 몸이 5까지 컸다. `tools/sizes.js`를 만들어 재고, **최대 3**으로 판을 다시 짰다.
  *재지 않는 것은 안 보인다* — 초록불이 켜져 있어도
- **2026-08-28** 프로젝트 폴더 이름을 바꾸니 배치모드가 *"Scripts have compiler errors"*로 죽었다.
  **로그에 `error CS`는 한 줄도 없었다** — `Library/Bee`의 절대경로 캐시였다.
  `Library`·`Temp`·`obj`·`UserSettings`를 지우면 된다. Unity Hub이 떠 있으면 rename 자체가 막히니 `robocopy /MOVE`.
  → 재사용 지식이라 통합 위키 `d:/Gcalen/wiki/unity.md`에도 적었다
- **2026-08-27** 움직임을 예측하기 쉽게 만들려고 "앞으로 한 칸씩만"으로 묶었더니
  **예산이 안 맞아 아무 판도 안 풀렸다.** 불편해 보이는 동작이 그 게임의 추진력일 수 있다

## 상태 (2026-08-28)

**1단계 킬 테스트 통과 → 2단계 수직 슬라이스 진행 중. 제목 SlimeEscape 확정.**

움직임은 한 걸음을 마디(옆으로 → 떨어짐 → 몸 변화)로 쪼개 굴린다. 수치는 게임 중 **K**로 돌려보고 저장한다.
🔴 판 6개 중 5개가 *오른쪽만 누르면* 풀린다 — 이게 지금 가장 위험한 가정이다. `wiki/todo.md` 참고.
