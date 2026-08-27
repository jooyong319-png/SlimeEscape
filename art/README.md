# _art_recovered — GIF에서 되살린 스프라이트

원본 작업 파일이 유실되고 GIF만 남은 것을 복구했다. **GIF는 무손실이라 픽셀 손실은 0이다.**
잃은 것은 레이어·타임라인 같은 작업 파일뿐이고, 그림 자체는 전부 남아 있었다.

| 폴더 / 파일 | 내용 | 프레임 | 칸 크기 |
|---|---|---|---|
| `slime_idle/` · `slime_idle_sheet.png` | 슬라임 대기 (물렁 출렁) | 4 | 63x52 |
| `slime_move/` · `slime_move_sheet.png` | 슬라임 이동 (오른쪽, 꼬리 끌림, 눈동자 슬릿) | 3 | 67x47 |
| `fire_idle/` · `fire_idle_sheet.png` | 불꽃 (일렁임 + 불티) | 4 | 84x114 |

- `*_sheet.png` — 가로 스프라이트시트. Unity Sprite Editor에서
  **Slice > Grid By Cell Size**에 위 칸 크기를 넣으면 잘린다
- 낱장 PNG도 폴더에 있다 (`00.png`~)
- 전부 **투명 배경 · 원본 해상도 1:1 · 10 FPS**
- 색은 `PALETTE.md` 참고

## ⚠️ 주의

- 🔴 **도트는 왼쪽을 보고 있다.** `slime_move`를 보면 **눈이 왼쪽, 꼬리가 오른쪽으로 끌린다** —
  왼쪽으로 가는 모습이다. 그래서 코드에서는 **오른쪽으로 갈 때 뒤집는다**
  (`GameController.SpriteFacesLeft`). 처음에 반대로 걸어서 좌우가 뒤집혀 보였다

- `slime_idle`(63x52)과 `slime_move`(67x47)는 **칸 크기가 다르다.**
  Unity에서 두 애니메이션을 이어 붙이려면 **피벗을 발밑(bottom-center)으로 맞춰야**
  전환할 때 안 튄다. 원본 GIF의 캔버스 위치는 서로 다른 시점에 뽑힌 것이라 신뢰할 수 없다
- 원본 GIF는 `d:/Make_game/*.gif`에 그대로 두었다 (지우지 말 것 — 이게 마지막 원본이다)
