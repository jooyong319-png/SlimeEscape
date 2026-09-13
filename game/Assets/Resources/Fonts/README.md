# 글꼴

`kr.ttf` = **Pretendard Regular** 에서 이 게임이 쓰는 글자만 잘라내고
이름을 **`SlimeEscape Sans`** 로 바꾼 것 (2026-09-12).

```
원본     Pretendard Regular v1.3.9 (alternative/Pretendard-Regular.ttf)
         Copyright (c) 2021, Kil Hyung-jin — github.com/orioncactus/pretendard
라이선스  SIL Open Font License 1.1   → OFL.txt
크기     2.6 MB → 180 KB (쓰는 글자 930자)
이름     SlimeEscape Sans (SlimeEscapeSans-Regular)
```

🟢 **재배포 가능하다.** itch.io 에 올려도 되고 저장소에도 올라간다.

## 🔴 이름을 바꾼 까닭 — 예약 이름

Pretendard 의 OFL 머리말에 **`with Reserved Font Name Pretendard`** 가 있다.
글자를 잘라낸 것은 OFL 이 말하는 *Modified Version* 이고, 3항이
*"Modified Version 은 예약 이름을 쓸 수 없다"* 고 한다.
그래서 글꼴 안의 이름(name 표 1·3·4·6·16…)을 `SlimeEscape Sans` 로 바꿨다.

**그대로 둔 것** — 지워야 하는 것이 아니라 지켜야 하는 것이다:

| name ID | 무엇 |
|---|---|
| 0 | 저작권 줄 |
| 7 | 상표 표시 ("Pretendard is a trademark of Kil Hyung-jin") |
| 9 · 10 | 디자이너 · 설명 (Inter · Noto Sans CJK 를 바탕으로 만들었다는 출처) |
| 13 · 14 | 라이선스 |

`tools/subset-font.py` 가 이름을 바꾸고, **예약 이름이 이름 칸에 남아 있으면 저장하지 않고 멈춘다.**

> ⚠️ 예전 갈무리11 은 예약 이름이 **없어서** 이름을 그대로 뒀었다.
> 글꼴을 바꿀 때마다 `OFL.txt` 머리말에 `Reserved Font Name` 이 있는지 **먼저** 본다.

## 🔴 2026-09-12 이전 — 도트 글꼴이었다

갈무리11(도트 글꼴)을 쓰고 있었다. rev.8 에 그림에서 도트를 버렸는데
글꼴만 남아 **글씨만 도트로 튀었다** (사장님: *"도트 디자인 다 빼야하는데"*).
그때 딸려 있던 두 가지도 같이 걷었다:

- `FontCheck.cs` 가 `HintedRaster`(픽셀 경계에 딱)로 못박던 것 → **`Smooth`**
  (보통 글꼴을 HintedRaster 로 두면 곡선이 계단진다)
- `Px()` 가 글자 크기를 **11의 배수**로 떨어뜨리던 것 → 그냥 정수 픽셀 (10 아래로는 안 내림)

그 전(09-02 이전)에는 윈도우 `malgun.ttf` 를 자른 것이라 **재배포 불가**였다.

## 다시 만들려면

```
pip install fonttools
python tools/subset-font.py <Pretendard-Regular.ttf 경로>
```

원본은 저장소에 안 넣는다 (2.6 MB). 공식 릴리스 zip 의 `public/static/alternative/Pretendard-Regular.ttf`.

글자 목록은 손으로 관리하지 않는다. **소스와 판 데이터에서 긁어온다** —
손으로 적으면 문구를 바꿨을 때 그 글자만 조용히 사라진다.

⚠️ 그래서 **한국어 문구를 새로 쓰면 다시 잘라야 한다.** 안 그러면
새로 쓴 글자가 빈칸으로 나온다. 에디터(윈도우)에서는 시스템 글꼴로
대체돼서 **안 드러나고, WebGL 빌드에서만 터진다.**

## OFL 지킬 것

배포할 때 **저작권 표시와 라이선스 전문을 같이** 넣어야 한다 (OFL 2항).
→ `OFL.txt` 가 그 파일이다 (Pretendard 저장소의 `LICENSE.txt` 그대로). 빌드에 같이 들어가야 한다.
