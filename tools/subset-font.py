# -*- coding: utf-8 -*-
# 🔴 폰트 파일 자체에서 안 쓰는 글자를 잘라낸다.
#
#   python tools/subset-font.py <원본.ttf>
#
# 왜 이 방식인가 (2026-08-29):
#   처음엔 유니티 임포터로 글자를 골라 구웠다(FontTextureCase.CustomSet).
#   용량은 줄었는데 **글자가 하나도 안 나왔다.**
#   🔴 유니티 정적 폰트는 fontSize를 못 바꾼다 — 코드가 21/16/13px을 지정하니 아무것도 안 그린다.
#   그래서 폰트는 **동적으로 두고**, 대신 TTF 자체를 줄인다. 둘 다 얻는다.
#
# 글자 목록은 손으로 관리하지 않는다. 소스와 판 데이터에서 긁어온다 —
# 손으로 적으면 문구를 바꿨을 때 그 글자만 조용히 사라진다.
import io, os, sys, glob

SRC = sys.argv[1] if len(sys.argv) > 1 else r'C:\Windows\Fonts\malgun.ttf'
OUT = 'game/Assets/Resources/Fonts/kr.ttf'

chars = set()
for path in glob.glob('game/Assets/Scripts/**/*.cs', recursive=True):
    chars |= set(io.open(path, encoding='utf-8', errors='ignore').read())
for path in glob.glob('game/Assets/Editor/**/*.cs', recursive=True):
    chars |= set(io.open(path, encoding='utf-8', errors='ignore').read())
levels = 'game/Assets/Resources/levels.json'
if os.path.exists(levels):
    chars |= set(io.open(levels, encoding='utf-8').read())

# 항상 넣는 것 — 기록 표와 숫자 서식이 쓴다
chars |= set('0123456789.,:;/%()[]-+*=#·×…?! ')
chars |= set('← → ↑ ↓ ●○')
chars = {c for c in chars if ord(c) > 31}

from fontTools import subset
opt = subset.Options()
opt.layout_features = ['*']
opt.name_IDs = ['*']
opt.notdef_outline = True
opt.recalc_bounds = True
opt.drop_tables += ['DSIG']

font = subset.load_font(SRC, opt)
subsetter = subset.Subsetter(options=opt)
subsetter.populate(text=''.join(sorted(chars)))
subsetter.subset(font)
subset.save_font(font, OUT, opt)

before = os.path.getsize(SRC) / 1048576.0
after = os.path.getsize(OUT) / 1048576.0
print('글자 %d자 · %.1f MB -> %.2f MB (%.0f%% 줄임)'
      % (len(chars), before, after, (1 - after / before) * 100))
