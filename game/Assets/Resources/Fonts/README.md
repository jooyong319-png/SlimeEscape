# 글꼴

`kr.ttf` = **갈무리11**(Galmuri11)에서 이 게임이 쓰는 글자만 잘라낸 것.

```
원본     Galmuri11 Regular  ·  Copyright (c) 2019-2025 Lee Minseo (quiple@quiple.dev)
라이선스  SIL Open Font License 1.1   → OFL.txt
크기     5.1 MB → 102 KB (쓰는 글자 814자만)
```

🟢 **재배포 가능하다.** itch.io에 올려도 되고 저장소에도 올라간다.

## 🔴 2026-09-02 이전에는 아니었다

전에는 윈도우 기본 글꼴(`malgun.ttf`)을 자른 것이었다. **재배포 불가**라
gitignore로 빼놓고 있었고, **이것 하나가 출시를 막는 유일한 항목**이었다.

옆 프로젝트(SalvageRun)가 이미 갈무리를 쓰고 있어서 그걸 가져왔다.
받으러 갈 필요도 없었다 — 물어보기 전엔 몰랐다.

## 다시 만들려면

```
python tools/subset-font.py <원본.ttf>
```

글자 목록은 손으로 관리하지 않는다. **소스와 판 데이터에서 긁어온다** —
손으로 적으면 문구를 바꿨을 때 그 글자만 조용히 사라진다.

⚠️ 그래서 **한국어 문구를 새로 쓰면 다시 잘라야 한다.** 안 그러면
새로 쓴 글자가 빈칸으로 나온다. 에디터(윈도우)에서는 시스템 글꼴로
대체돼서 **안 드러나고, WebGL 빌드에서만 터진다.**

## 🔴 도트 글꼴이라 지켜야 하는 것 둘

**1. 뭉개면 안 된다.** 유니티는 기본이 `Smooth`(안티에일리어싱)라 획이
두 픽셀에 번진다. `HintedRaster`로 둬야 픽셀 경계에 딱 떨어진다.
→ `Assets/Editor/FontCheck.cs` 가 자동으로 맞춘다. 손대지 않아도 된다.

**2. 크기는 11의 배수.** 갈무리11은 11픽셀에 맞춰 그려졌다. 13px·20px 같은
어중간한 크기로 쓰면 획이 1.2픽셀이 되어 글자만 흐려진다 — 화면을 전부
도트로 맞춰놨는데 글자에서 티가 나면 다 무너진다.
→ 코드에서 `Px()` 를 통과시킨다. `fontSize = Px(...)` 로 쓸 것.

## OFL 지킬 것

배포할 때 **저작권 표시와 라이선스 전문을 같이** 넣어야 한다 (OFL 2항).
→ `OFL.txt` 가 그 파일이다. 빌드에 같이 들어가야 한다.

⚠️ 자른 것은 OFL에서 말하는 *Modified Version* 이다. 3항이 *"Modified Version은
예약 이름(Reserved Font Name)을 쓸 수 없다"*고 한다. **갈무리는 예약 이름이 없다** —
그래서 `Galmuri11` 이라는 이름을 그대로 둬도 된다.

두 군데서 확인했다 (2026-09-02):

```
글꼴 파일의 name 표      Copyright (c) 2019-2025 Lee Minseo (quiple@quiple.dev)
저장소 ofl.md 머리말     Copyright © 2019–2025 Lee Minseo (quiple@quiple.dev)
```

둘 다 `with Reserved Font Name` 절이 **없다.**

> ⚠️ openfontlicense.org 에서 받는 건 **빈 서식**이다 — 머리말이
> `Copyright (c) <dates>, <Copyright Holder> ... with Reserved Font Name <...>` 처럼
> 꺾쇠로 되어 있다. 그 줄이 있다고 예약 이름이 있는 게 아니다. 모든 글꼴에
> 똑같이 배포되는 본보기다. 여기 `OFL.txt` 는 실제 저작권 줄로 채워둔 것이다.
>
> 원본은 저장소의 `ofl.md` (한국어판은 `ofl-ko.md`). 파일 이름이 `OFL.txt` 가
> 아니라서 처음에 못 찾았다.
