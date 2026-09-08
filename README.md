# SlimeEscape

**몸이 사슬처럼 길어지는 격자 퍼즐.** 유적을 내려가며 파인 홈을 제 몸으로 정확히 채워 연다.
Unity 6000.3.22f1 · itch.io 웹 · 시작 2026-08-27 · **rev.12-a (2026-09-08)**

## 새 컴퓨터에서 시작하기

```bash
git clone https://github.com/jooyong319-png/SlimeEscape.git
```

필요한 것: **Unity 6000.3.22f1** (Hub에서 같은 판) · **Node 18↑** · **Python 3**

🔴 **씬은 자산으로 안 들고 다닌다.** 유니티로 `game/` 을 처음 열면
메뉴 **`SlimeEscape › 씬 다시 만들기`** 를 한 번 누른다 (`ProjectBootstrap.cs` 가 코드로 만든다).

```bash
node tools/verify.js       # 판 40개가 성한지 (유니티 없이 도는 검사)
python tools/braces.py     # C# 괄호·끊긴 문자열 검사
```

## 🔴 판단이 갈리면

**`docs/project-brief.md` 로 결정한다.** 맥락은 `wiki/` —
처음이면 `wiki/index.md` (지금 상태 한눈에) → `wiki/SCHEMA.md` → `wiki/todo.md` 순.
`wiki/todo.md` 맨 아래에 **이미 밟은 덫 45개**가 있다. 손대기 전에 훑어볼 것.

## 규칙 (전부)

1. 머리를 **상하좌우 한 칸**. 몸이 따라온다
2. 🔴 **중력 있음.** 몸이 통째로 떨어진다 — **위로 k칸 오르려면 길이가 k+1 이상**
3. 조각을 먹으면 **길이 +1**. 줄어드는 건 없다
4. 벽·자기 몸에 부딪히는 걸음은 **막힌다**. 죽지 않는다 — 못 갈 뿐
5. 🔴 **표시된 홈을 몸으로 정확히 채우면** 열린다. 남아도 모자라도 안 된다
6. **심(心)** `*` `%` `@` — 머리가 마지막에 있어야 할 칸
7. 되돌리기 무제한 (`Z`) · 처음부터 (`R`)
8. 🔴 **홈을 다 채우면 그 자리에서 판이 끝난다** — 별은 **그 전에** 주워야 한다

## 판 기호

```
#  벽              .  빈칸            S  시작
+  조각            o  별 (몸 안 늘어남)
=  홈1  *  홈1의 심        -  홈2  %  홈2의 심        ~  홈3  @  홈3의 심
1 2 3  문벽 (같은 번호 홈을 채우면 사라진다)
B  미는 상자       P Q / p q  포탈 짝
I  얼음 (밟는 바닥 — 좌우로 미끄러진다)
X  금 간 바닥 (딛고 서지만 지나가면 무너진다)
:  무중력 구역 — ⛔ 지금 안 쓴다
```

## 설계 원칙 ↔ 코드 위치

| 원칙 | 어디에 강제돼 있나 |
|---|---|
| 🔴 규칙은 **두 벌**이고 늘 같아야 한다 | `Scripts/SnakeEngine.cs` ↔ `tools/engine.js` |
| 길이가 안 맞는 판은 못 만든다 | `SnakeEngine.Parse` 가 예외를 던진다 |
| 씬에 손으로 배치한 것 없음 | `Editor/ProjectBootstrap.cs` 가 코드로 만든다 |
| 🔴 연출이 칸을 안 속인다 | `SnakeController.Animate` — 끌림은 **시간차**로만 |

## 자주 쓰는 명령

```bash
# 판 만들기
ICE=1   node tools/hardpool.js      # 얼음 못 (CRACK=1 · BOX=1 · HARD=1 도 있다)
        node tools/finale.js        # 기믹 넷이 한 판에 (한 판 나올 때마다 저장)
AT=end WRITE=1 node tools/chapter.js 2    # 굴 2(상자)를 짓는다
WRITE=1 node tools/swap.js 1-5 1-11       # 지목한 판만 갈아끼운다

# 검사
node tools/verify.js                # 수순·최단·3별 가능
node tools/restar.js                # 별 걸음·커트를 다시 잰다 (못 받는 별을 찾는다)
python tools/braces.py
```

## Unity 배치 (컴파일 · 검사 · 빌드)

```bash
U="C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe"
"$U" -batchmode -quit -nographics -projectPath game -logFile compile.log          # 컴파일만
"$U" -batchmode -quit -nographics -projectPath game -logFile conform.log \
     -executeMethod SlimeEscape.EditorTools.ConformanceCheck.Run                  # 규칙 두 벌 대조
"$U" -batchmode -quit -nographics -projectPath game -logFile build.log \
     -executeMethod SlimeEscape.EditorTools.BuildWeb.Run                          # 웹 빌드
python tools/zipweb.py                                                            # 🔴 itch.io 용 zip
```

⚠️ **유니티 에디터가 떠 있으면 배치모드가 못 들어간다.** `tools/check.ps1` 이 이걸 먼저 본다.

## 웹 빌드 확인

```bash
cd build/web && python -m http.server 8777       # http://127.0.0.1:8777
```

📛 **`index.html` 을 파일로 바로 열면 안 뜬다** — 브라우저가 `.wasm` 읽기를 막는다.
📛 **zip 을 PowerShell `Compress-Archive` 로 묶으면 안 된다** — 경로에 역슬래시가 들어가
itch.io 에서 하위 폴더가 통째로 404 난다. 반드시 `python tools/zipweb.py`.
(`wiki/todo.md` 덫 43 · `docs/distribution.md`)

## ⚠️ 손대기 전에 알아야 할 것

- 🔴 **규칙은 두 벌이다.** 한쪽만 고치면 적합성 검사가 깨진다. **양쪽을 같이 고칠 것**
- **판당 조각은 26개까지** — 비트마스크 한계. 양쪽 엔진 공통
- 🔴 **판 자료를 일괄로 고치기 전에 사본을 뜬다.** 전에 판 일곱 개를 날렸다
- 🔴 **어렵다/쉽다는 도구가 못 잰다.** 여덟 번 틀렸다 — 사장님이 해보고 판단하신다.
  도구는 객관적인 것만 거른다 (풀린다 · 최단해 하나 · 기믹이 실제로 발동 · 안 겹침)
- HUD 가 `OnGUI` 다. **임시다** — 이전 프로젝트가 이걸 끝까지 끌고 갔다
