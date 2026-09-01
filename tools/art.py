# -*- coding: utf-8 -*-
"""
도트를 굽는다.  python tools/art.py

🔴 한 칸 = 32×32 픽셀로 못박는다 (09-02).
   크기가 섞이면 **픽셀 크기가 달라 보인다** — 파이프라인은 섞어도 돌아가지만
   눈에는 바로 티가 난다. 그림을 새로 그리시면 전부 같은 크기로 맞춘다.

그리는 방식:
   바탕(경사·명암)은 코드가 깔고, **세부는 손으로 찍는다.**
   손으로 찍는 자리는 DETAIL 목록에 (x, y, 색) 으로 적혀 있다.
   자연물 질감을 흉내내지 않는다 — 5단 명암 + 또렷한 윤곽으로 간다.
   흉내내다 실패한 것보다 **일부러 그런 것**으로 보이는 쪽이 낫다.

나오는 곳: game/Assets/Resources/Art/*.png  (+ tools/art-preview.png)
"""
import zlib, struct, io, os

N = 32                                    # 한 칸의 픽셀 수
OUT = 'd:/Make_game/SlimeEscape/game/Assets/Resources/Art'
PREVIEW = 'd:/Make_game/SlimeEscape/tools/art-preview.png'

# ---------------------------------------------------------------- 색
# 🔴 층마다 5단. 단이 적으면 밋밋하고 많으면 뭉개진다.
PAL = {
    ' ': None,                                    # 비침
    # 돌 — 지형
    '1': (0x16, 0x1f, 0x1a), '2': (0x22, 0x2e, 0x28), '3': (0x2f, 0x3e, 0x37),
    '4': (0x3b, 0x4c, 0x43), '5': (0x50, 0x66, 0x59), '6': (0x6b, 0x85, 0x74),
    # 빈 칸 — 굴 속 공기
    'f': (0x0f, 0x16, 0x13), 'g': (0x14, 0x1d, 0x19), 'h': (0x1a, 0x25, 0x20),
    # 슬라임 — 나
    'p': (0x3d, 0x8a, 0x69), 'q': (0x5e, 0xac, 0x86), 'r': (0x8d, 0xce, 0xb0),
    's': (0xb3, 0xe4, 0xcb), 't': (0xdc, 0xf5, 0xe8),
    # 놋쇠 — 기계(열쇠 · 심 · 틀)
    'j': (0x4f, 0x3a, 0x16), 'k': (0x8a, 0x67, 0x27), 'l': (0xc9, 0x9a, 0x46),
    'm': (0xf0, 0xc8, 0x6b), 'n': (0xff, 0xf0, 0xc4),
    # 조각 — 주황
    'u': (0x7a, 0x40, 0x06), 'v': (0xc4, 0x69, 0x04),
    'w': (0xf3, 0x8a, 0x04), 'x': (0xff, 0xc0, 0x5c),
    # 별 — 놋쇠 기계와 안 섞이게 창백한 금빛
    'A': (0x8a, 0x76, 0x3a), 'B': (0xd8, 0xc2, 0x76),
    'C': (0xf5, 0xe6, 0xae), 'D': (0xff, 0xfb, 0xef),
    # 받침대 — 홈도 지형도 아니다. 확실히 푸른 돌
    'E': (0x2a, 0x33, 0x44), 'F': (0x3d, 0x4a, 0x63),
    'G': (0x55, 0x66, 0x86), 'H': (0x86, 0x9b, 0xc2),
    # 🔴 빛. 네 자리는 투명도까지 준다 — 도트에서 빛은 **단을 나눠** 번지게 한다.
    #    부드럽게 깔면 도트가 아니게 되고, 단이 너무 굵으면 고리로 보인다.
    'i': (0xf0, 0xc8, 0x6b, 0x66), 'o': (0xf0, 0xc8, 0x6b, 0x38),
    'y': (0xf0, 0xc8, 0x6b, 0x18),
    'I': (0xf5, 0xe6, 0xae, 0x60), 'O': (0xf5, 0xe6, 0xae, 0x30),
    'Y': (0xf5, 0xe6, 0xae, 0x14),
}


def blank(ch=' '):
    return [[ch] * N for _ in range(N)]


def stamp(g, pts):
    """손으로 찍는 자리. (x, y, 색) 목록."""
    for x, y, c in pts:
        if 0 <= x < N and 0 <= y < N:
            g[y][x] = c


def box(g, x0, y0, x1, y1, c):
    for y in range(max(0, y0), min(N, y1 + 1)):
        for x in range(max(0, x0), min(N, x1 + 1)):
            g[y][x] = c


# ---------------------------------------------------------------- 돌
def wall(moss=False):
    """
    벽 한 칸. 🔴 **네 변이 맞물려야** 한다 — 옆 칸과 이어 붙기 때문이다.
    그래서 테두리는 좌우가 같은 규칙, 위아래가 같은 규칙으로 둔다.
    """
    g = blank('3')
    box(g, 0, 0, N - 1, 1, '5')          # 윗면 — 빛을 받는다
    box(g, 0, 2, N - 1, 3, '4')
    box(g, 0, N - 2, N - 1, N - 1, '2')  # 아랫면 — 가라앉는다
    box(g, 0, 0, 0, N - 1, '4')          # 왼쪽이 조금 밝다
    box(g, N - 1, 0, N - 1, N - 1, '2')  # 오른쪽이 그늘
    box(g, 0, N - 1, N - 1, N - 1, '1')  # 칸 사이 이음매

    # 손으로 찍은 금 — 대각선으로 흘러야 금처럼 보인다.
    # 🔴 앞서 십자로 찍었더니 금이 아니라 **무늬**로 보였다 (09-02).
    crack = [(7, 6), (8, 7), (8, 8), (9, 9), (9, 10), (10, 11), (11, 12)]
    stamp(g, [(x, y, '2') for x, y in crack])
    stamp(g, [(x + 1, y, '4') for x, y in crack])
    crack2 = [(22, 17), (22, 18), (21, 19), (21, 20), (20, 21), (20, 22)]
    stamp(g, [(x, y, '2') for x, y in crack2])
    stamp(g, [(x + 1, y, '4') for x, y in crack2])

    #  🔴 얼룩을 네모로 찍었더니 돌이 아니라 **무늬**로 보였다 (09-02).
    #     점을 흩어 놓는다 — 덩어리지지 않게 자리를 손으로 골랐다.
    stamp(g, [(x, y, '4') for x, y in
              [(15, 7), (16, 7), (17, 8), (16, 9), (5, 18), (6, 19), (7, 18),
               (26, 20), (27, 21), (13, 15), (14, 16), (24, 5), (3, 11)]])
    stamp(g, [(x, y, '2') for x, y in
              [(25, 9), (26, 10), (12, 24), (13, 25), (14, 24), (18, 13),
               (19, 14), (4, 26), (28, 15), (9, 21), (21, 27)]])

    if moss:
        box(g, 0, 0, N - 1, 1, '6')
        for x in (2, 3, 7, 8, 9, 14, 15, 19, 23, 24, 28):
            stamp(g, [(x, 2, '6'), (x, 3, '5')])
        for x in (5, 11, 17, 26):
            stamp(g, [(x, 4, '5')])
    return g


def wall_top():
    """돌 **윗면**. 🔴 "여기 딛고 설 수 있다"는 신호. 위쪽만 쓰고 나머지는 비운다."""
    g = blank(' ')
    box(g, 0, 0, N - 1, 2, '6')
    box(g, 0, 3, N - 1, 4, '5')
    for x in (3, 4, 9, 10, 16, 21, 22, 27):
        stamp(g, [(x, 5, '5')])
    return g


def floor_():
    """빈 칸. 🔴 여기도 이어 붙는다. **눈에 띄면 안 된다** — 배경이다."""
    g = blank('g')
    box(g, 0, 0, N - 1, 3, 'h')          # 위가 아주 조금 밝다
    box(g, 0, N - 4, N - 1, N - 1, 'f')  # 아래가 가라앉는다
    for x, y in [(6, 9), (7, 9), (20, 14), (21, 14), (13, 22), (26, 6)]:
        stamp(g, [(x, y, 'f')])
    return g


# ---------------------------------------------------------------- 슬라임
def _blob(pad, top_flat=False):
    """둥근 덩어리 하나. 마디·머리가 같은 몸에서 나온다."""
    g = blank(' ')
    c = (N - 1) / 2.0
    rad = c - pad
    for y in range(N):
        for x in range(N):
            dx, dy = x - c, y - c
            d = (dx * dx + dy * dy) ** 0.5
            if d > rad:
                continue
            #  빛은 위에서 온다 — 위가 밝고 아래가 어둡다
            t = (dy + rad) / (2 * rad)
            g[y][x] = 'r' if t < 0.55 else ('q' if t < 0.82 else 'p')
            if d > rad - 1.2:
                g[y][x] = 'p'
    return g


def body():
    g = _blob(2.5)
    #  손으로 얹는 빛 한 점 — 이거 하나로 **젤리**가 된다
    for x0, y0, w, h in [(9, 7, 5, 2), (8, 9, 3, 1)]:
        box(g, x0, y0, x0 + w, y0 + h, 's')
    box(g, 10, 7, 12, 7, 't')
    return g


def head():
    g = body()
    #  머리는 조금 더 밝다. 어디가 앞인지 색으로도 말해준다.
    for y in range(N):
        for x in range(N):
            if g[y][x] == 'q':
                g[y][x] = 'r'
    return g


def link():
    """마디 **사이**. 🔴 몸이 끊겨 보이면 안 된다 — 위아래는 몸 굵기에 맞춘다."""
    g = blank(' ')
    #  🔴 몸의 결과 같아야 한다 — 위가 밝고 아래가 어둡다.
    #     띠를 뚝 잘라 놓으면 마디와 이음매가 딴 물건으로 보인다.
    box(g, 0, 3, N - 1, N - 4, 'q')
    box(g, 0, 3, N - 1, 3, 'p')
    box(g, 0, N - 4, N - 1, N - 4, 'p')
    box(g, 0, 4, N - 1, 13, 'r')
    box(g, 0, 6, N - 1, 8, 's')
    box(g, 0, 20, N - 1, N - 5, 'p')
    return g


def key():
    """머리에 박힌 열쇠. 위아래로 긴 마름모. 번쩍임은 코드가 입힌다."""
    g = blank(' ')
    cx = N // 2
    for y in range(N):
        #  |x|/w + |y|/h <= 1  가 마름모
        h = (N - 2) / 2.0
        w = (N - 2) / 2.0 * 0.52
        span = w * (1 - abs(y - (N - 1) / 2.0) / h)
        if span <= 0:
            continue
        for x in range(N):
            d = abs(x - (N - 1) / 2.0)
            if d > span:
                continue
            g[y][x] = 'l'
            if d > span - 1.1:
                g[y][x] = 'k'
            elif y < N * 0.45:
                g[y][x] = 'm'
    box(g, cx - 1, 9, cx, 13, 'n')       # 손으로 얹은 빛
    return g


def core():
    """
    열쇠 **구멍**. 🔴 머리에 박힌 열쇠와 **같은 마름모**여야 짝이 맞는다.
    앞서 네모로 그렸더니 구멍이 아니라 갈색 상자로 보였다 (09-02).
    열쇠는 꽉 찼으니 구멍은 비어 있어야 한다 — 테만 놋쇠, 속은 어둡게.
    """
    g = blank(' ')
    cy = (N - 1) / 2.0
    h = (N - 2) / 2.0
    w = h * 0.62
    for y in range(N):
        span = w * (1 - abs(y - cy) / h)
        if span <= 0:
            continue
        for x in range(N):
            d = abs(x - cy)
            if d > span:
                continue
            if d > span - 2.6:
                g[y][x] = 'l'          # 놋쇠 테
                if y < N * 0.42:
                    g[y][x] = 'm'      # 위쪽 테가 빛을 받는다
            else:
                g[y][x] = 'j'          # 파인 속
    return g


def food():
    """조각. 🔴 별과 확실히 달라야 한다 — 이건 먹으면 몸이 는다."""
    g = blank(' ')
    c = (N - 1) / 2.0
    for y in range(N):
        for x in range(N):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d > 9.5:
                continue
            g[y][x] = 'w' if d < 8.2 else 'v'
            if (y - c) < -3 and d < 7:
                g[y][x] = 'x'
    box(g, 12, 9, 14, 10, 'x')
    stamp(g, [(c_, 22, 'u') for c_ in range(13, 19)])
    return g



def _glow(ramp):
    """
    빛. 🔴 도트에서 빛은 **단을 나눠** 번지게 한다 — 부드럽게 깔면 도트가 아니게 된다.
    바깥 단은 격자무늬로 성기게 찍어(디더) 단 사이가 뚝 끊겨 보이지 않게 한다.
    """
    g = blank(' ')
    c = (N - 1) / 2.0
    for y in range(N):
        for x in range(N):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if d < 5.5:
                g[y][x] = ramp[0]
            elif d < 8.5:
                g[y][x] = ramp[1]
            elif d < 11.5:
                #  디더 — 두 칸에 한 번씩만 찍는다
                if (x + y) % 2 == 0:
                    g[y][x] = ramp[1]
                else:
                    g[y][x] = ramp[2]
            elif d < 14.5:
                if (x + y) % 2 == 0:
                    g[y][x] = ramp[2]
    return g


def key_glow():
    """머리 열쇠 뒤에 번지는 빛. 밝기는 코드가 박자에 맞춰 올렸다 내린다."""
    return _glow('ioy')


def star_glow():
    return _glow('IOY')


def star():
    """
    별. 🔴 조각(둥근 것)과 **모양부터** 달라야 한다 — 먹어도 몸이 안 늘기 때문이다.
    뾰족한 네 갈래로 둔다. 둥근 것 옆에 두면 한눈에 갈린다.
    """
    g = blank(' ')
    c = (N - 1) / 2.0
    for y in range(N):
        for x in range(N):
            dx, dy = abs(x - c), abs(y - c)
            #  마름모를 세로·가로로 두 번 겹쳐 네 갈래를 만든다
            v = dx / 3.4 + dy / 13.0
            h = dx / 13.0 + dy / 3.4
            m = min(v, h)
            if m > 1.0:
                continue
            g[y][x] = 'B' if m > 0.72 else ('C' if m > 0.34 else 'D')
            if m > 0.94:
                g[y][x] = 'A'
    return g


def pad():
    """받침대. 🔴 홈이 **아니다** — 여기 놓은 몸은 점수가 안 되고 계단만 된다."""
    g = blank(' ')
    box(g, 1, 12, N - 2, N - 3, 'F')
    box(g, 1, 12, N - 2, 13, 'G')
    box(g, 1, N - 4, N - 2, N - 3, 'E')
    box(g, 1, 12, 2, N - 3, 'G')
    box(g, N - 3, 12, N - 2, N - 3, 'E')
    for x in (6, 7, 15, 16, 24):          # 손으로 찍은 이음매
        box(g, x, 15, x, N - 5, 'E')
    return g


def pad_top():
    g = blank(' ')
    box(g, 0, 0, N - 1, 2, 'H')
    box(g, 0, 3, N - 1, 3, 'G')
    return g


def spent():
    """
    두고 온 몸. 🔴 몸이 **돌이 된 것**이라, 모양은 마디 그대로 두고 색만 돌로 간다.
    딛고 설 수 있으니 지형처럼 보여야 한다 — 모양까지 바꾸면 다른 물건이 된다.
    """
    g = _blob(2.5)
    swap = {'p': '2', 'q': '3', 'r': '4', 's': '5', 't': '6'}
    for y in range(N):
        for x in range(N):
            if g[y][x] in swap:
                g[y][x] = swap[g[y][x]]
    return g


def slot():
    """홈 틀 **안 바닥**. 눈에 안 띄어야 한다 — 여기 들어가는 건 몸이다."""
    g = blank('2')
    box(g, 0, 0, N - 1, 1, '1')
    for x, y in [(8, 11), (9, 11), (20, 19), (21, 19), (14, 6), (25, 24), (4, 22)]:
        stamp(g, [(x, y, '1')])
    return g


def rail():
    """
    홈 틀의 레일 한 마디. 🔴 **위쪽 변**에 그린다 — 코드가 돌려서 네 변에 쓴다.
    변마다 따로 그리면 네 장이 되고, 넷이 조금씩 어긋나면 틀이 삐뚤어 보인다.
    """
    g = blank(' ')
    box(g, 0, 0, N - 1, 0, 'k')
    box(g, 0, 1, N - 1, 2, 'l')
    box(g, 0, 3, N - 1, 3, 'm')     # 안쪽 모서리가 빛을 받는다
    box(g, 0, 4, N - 1, 4, 'k')
    for x in range(2, N, 6):        # 손으로 찍은 못
        stamp(g, [(x, 2, 'm')])
    return g


def gem():
    """꺾이는 모서리의 장식. 머리 열쇠와 **같은 마름모** — 열쇠와 자물쇠가 한집안이다."""
    g = blank(' ')
    c = (N - 1) / 2.0
    for y in range(N):
        for x in range(N):
            m = abs(x - c) / 9.0 + abs(y - c) / 14.0
            if m > 1.0:
                continue
            g[y][x] = 'l' if m > 0.62 else 'm'
            if m > 0.9:
                g[y][x] = 'k'
    return g


def node(locked=False):
    """판 고르기 지도의 마디. 굴 입구처럼 — 어두운 돌에 위쪽만 밝다."""
    g = blank(' ')
    lo, mid, hi = ('1', '2', '3') if locked else ('2', '3', '5')
    box(g, 1, 1, N - 2, N - 2, mid)
    box(g, 1, 1, N - 2, 3, hi)
    box(g, 1, N - 4, N - 2, N - 2, lo)
    box(g, 1, 1, 2, N - 2, mid if locked else '4')
    box(g, N - 3, 1, N - 2, N - 2, lo)
    return g



# ---------------------------------------------------------------- 애니
# 🔴 여러 장을 구워 `이름_0.png` `이름_1.png` ... 로 두면 코드가 순서대로 넘긴다.
#    슬라임은 코드가 직접 움직이니 여기 없다 (09-02 사장님: "슬라임은 나중에").
#
#    움직임은 **작게**. 퍼즐 게임에서 배경이 크게 흔들리면 판을 읽는 눈이 흩어진다.
#    "살아 있다"만 알려주면 된다.

def food_frames(n=6):
    """조각 — 빛나는 점이 표면을 훑고 지나간다. 위아래로 한 픽셀씩 뜬다."""
    out = []
    for i in range(n):
        g = blank(' ')
        c = (N - 1) / 2.0
        lift = [0, -1, -1, 0, 1, 1][i % 6]
        for y in range(N):
            for x in range(N):
                d = ((x - c) ** 2 + (y - c - lift) ** 2) ** 0.5
                if d > 9.5:
                    continue
                g[y][x] = 'w' if d < 8.2 else 'v'
                if (y - c - lift) < -3 and d < 7:
                    g[y][x] = 'x'
        #  빛나는 점이 왼쪽 위 -> 오른쪽 위로 훑는다
        hx = 10 + i
        box(g, hx, 9 + lift, hx + 2, 10 + lift, 'x')
        stamp(g, [(hx, 9 + lift, 'x'), (hx + 1, 8 + lift, 'x')])
        stamp(g, [(cx_, 22 + lift, 'u') for cx_ in range(13, 19)])
        out.append(g)
    return out


def star_frames(n=6):
    """별 — 갈래가 길어졌다 짧아진다. 반짝임은 길이로 준다."""
    out = []
    for i in range(n):
        g = blank(' ')
        c = (N - 1) / 2.0
        long_ = [13.0, 14.5, 15.5, 14.5, 13.0, 11.5][i % 6]
        for y in range(N):
            for x in range(N):
                dx, dy = abs(x - c), abs(y - c)
                v = dx / 3.4 + dy / long_
                h = dx / long_ + dy / 3.4
                m = min(v, h)
                if m > 1.0:
                    continue
                g[y][x] = 'B' if m > 0.72 else ('C' if m > 0.34 else 'D')
                if m > 0.94:
                    g[y][x] = 'A'
        out.append(g)
    return out


def core_frames(n=6):
    """열쇠 구멍 — 놋쇠 테에 빛이 한 바퀴 돈다. 여기로 오라는 신호다."""
    out = []
    for i in range(n):
        g = blank(' ')
        cy = (N - 1) / 2.0
        h = (N - 2) / 2.0
        w = h * 0.62
        lit = i / float(n)                     # 빛이 도는 자리 (위에서 아래로)
        for y in range(N):
            span = w * (1 - abs(y - cy) / h)
            if span <= 0:
                continue
            for x in range(N):
                d = abs(x - cy)
                if d > span:
                    continue
                if d > span - 2.6:
                    #  세로 위치가 빛과 가까우면 밝아진다
                    near = abs((y / float(N)) - lit)
                    g[y][x] = 'm' if near < 0.16 else 'l'
                else:
                    g[y][x] = 'j'
        out.append(g)
    return out


def gem_frames(n=6):
    """틀 모서리 장식 — 아주 옅게 숨만 쉰다. 틀은 배경이라 크게 움직이면 안 된다."""
    out = []
    for i in range(n):
        g = blank(' ')
        c = (N - 1) / 2.0
        grow = [0.0, 0.06, 0.10, 0.06, 0.0, -0.04][i % 6]
        for y in range(N):
            for x in range(N):
                m = abs(x - c) / (9.0 * (1 + grow)) + abs(y - c) / (14.0 * (1 + grow))
                if m > 1.0:
                    continue
                g[y][x] = 'l' if m > 0.62 else 'm'
                if m > 0.9:
                    g[y][x] = 'k'
        out.append(g)
    return out


def glow_frames(ramp, n=6):
    """빛 — 크기가 숨을 쉰다. 열쇠·별 뒤에 깔린다."""
    out = []
    for i in range(n):
        g = blank(' ')
        c = (N - 1) / 2.0
        k = [1.0, 1.06, 1.12, 1.06, 1.0, 0.95][i % 6]
        for y in range(N):
            for x in range(N):
                d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5 / k
                if d < 5.5:
                    g[y][x] = ramp[0]
                elif d < 8.5:
                    g[y][x] = ramp[1]
                elif d < 11.5:
                    g[y][x] = ramp[1] if (x + y) % 2 == 0 else ramp[2]
                elif d < 14.5 and (x + y) % 2 == 0:
                    g[y][x] = ramp[2]
        out.append(g)
    return out


FRAMES = {
    'food': food_frames(),
    'star': star_frames(),
    'core': core_frames(),
    'gem': gem_frames(),
    'key_glow': glow_frames('ioy'),
    'star_glow': glow_frames('IOY'),
}

TILES = {
    'wall': wall(moss=True),
    'wall_top': wall_top(),
    'floor': floor_(),
    'body': body(),
    'head': head(),
    'link': link(),
    'key': key(),
    'core': core(),
    'food': food(),
    'star': star(),
    'star_glow': star_glow(),
    'key_glow': key_glow(),
    'pad': pad(),
    'pad_top': pad_top(),
    'spent': spent(),
    'slot': slot(),
    'rail': rail(),
    'gem': gem(),
    'node': node(),
    'node_lock': node(locked=True),
}


# ---------------------------------------------------------------- 굽기
def _png(path, w, h, pixel):
    #  🔴 bytes 를 이어붙이면 **제곱으로** 느려진다 — 한번 멈춰서 알았다.
    #     bytearray 에 밀어넣는다.
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw += pixel(x, y)

    def chunk(tag, data):
        d = tag + data
        return struct.pack('>I', len(data)) + d + struct.pack('>I', zlib.crc32(d) & 0xffffffff)

    io.open(path, 'wb').write(
        b'\x89PNG\r\n\x1a\n'
        + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
        + chunk(b'IDAT', zlib.compress(bytes(raw), 6))
        + chunk(b'IEND', b''))


def _rgba(c):
    """색은 (r,g,b) 또는 (r,g,b,a). 없으면 완전히 비침."""
    if not c:
        return b'\x00\x00\x00\x00'
    return bytes(c) if len(c) == 4 else bytes(c) + b'\xff'


def bake(name, g):
    _png(os.path.join(OUT, name + '.png'), N, N,
         lambda x, y: _rgba(PAL[g[y][x]]))


def preview(zoom=5, gap=1, per=11):
    shown = dict(TILES)
    for k, v in FRAMES.items():
        shown[k + '(1/%d)' % len(v)] = v[2]
    names = list(shown)
    rows_ = (len(names) + per - 1) // per
    w = (N + gap) * per * zoom
    h = (N + gap) * rows_ * zoom
    bg = (0x0d, 0x13, 0x11)

    def px(X, Y):
        col = (X // zoom) // (N + gap)
        row = (Y // zoom) // (N + gap)
        ox = (X // zoom) % (N + gap)
        oy = (Y // zoom) % (N + gap)
        i = row * per + col
        if col >= per or i >= len(names) or ox >= N or oy >= N:
            return bytes(bg) + b'\xff'
        c = PAL[shown[names[i]][oy][ox]]
        if not c:
            return bytes(bg) + b'\xff'
        if len(c) == 4:      # 미리보기에서는 바탕에 얹어 보여준다
            a = c[3] / 255.0
            c = tuple(int(c[k] * a + bg[k] * (1 - a)) for k in range(3))
        return bytes(c) + b'\xff'
    _png(PREVIEW, w, h, px)


def check(name, g):
    bad = [i for i, r in enumerate(g) if len(r) != N]
    if bad or len(g) != N:
        raise SystemExit('%s 크기 틀림: 줄 %s / 세로 %d' % (name, bad, len(g)))


if __name__ == '__main__':
    #  \U0001f534 애니가 있는 자리는 한 장짜리를 **안 굽는다.**
    #     `star.png` 와 `star_0.png` 가 같이 있으면 어느 쪽이 쓰일지 헷갈린다.
    for n in FRAMES:
        TILES.pop(n, None)
        p = os.path.join(OUT, n + '.png')
        for q in (p, p + '.meta'):
            if os.path.exists(q):
                os.remove(q)

    for n, gs in FRAMES.items():
        for i, g in enumerate(gs):
            check(n, g)
            bake('%s_%d' % (n, i), g)

    for n, g in TILES.items():
        check(n, g)
        bake(n, g)
    preview()
    print('구움: 한 장짜리 %d개 + 애니 %d개(각 %d장) -> %s'
          % (len(TILES), len(FRAMES), len(next(iter(FRAMES.values()))), OUT))
    print('미리보기 -> %s' % PREVIEW)
