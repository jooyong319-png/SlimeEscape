# -*- coding: utf-8 -*-
"""웹 빌드를 itch.io 에 올릴 zip 으로 묶는다.

🔴 **PowerShell 의 Compress-Archive 를 쓰면 안 된다** (2026-09-08).
   윈도우 PowerShell 5.1 은 zip 안의 경로를 `Build\\web.data` 처럼 **역슬래시**로 적는다.
   ZIP 규격은 `/` 라서, 받는 쪽(itch.io)은 그걸 폴더가 아니라 **파일 이름 한 덩어리**로 읽는다.
   그러면 index.html 은 열리는데 `Build/web.wasm` 은 404 가 나고,
   화면엔 스타일도 캔버스도 없는 **회색 바탕에 글씨 하나**만 남는다.
   ⚠️ 로컬 서버(폴더 그대로)에서는 멀쩡히 돌아서 **빌드 문제로 착각하기 딱 좋다.**

   python tools/zipweb.py
"""
import os, sys, zipfile

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'build', 'web')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'build', 'SlimeEscape-web.zip')
SRC = os.path.normpath(SRC); OUT = os.path.normpath(OUT)

if not os.path.isfile(os.path.join(SRC, 'index.html')):
    print('index.html 이 없다:', SRC); sys.exit(1)

if os.path.exists(OUT):
    os.remove(OUT)

n = 0
with zipfile.ZipFile(OUT, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(SRC):
        for f in files:
            full = os.path.join(root, f)
            #  🔴 zip 안 경로는 **반드시 슬래시**로
            rel = os.path.relpath(full, SRC).replace(os.sep, '/')
            z.write(full, rel)
            n += 1

#  🔴 묶고 나서 **정말 슬래시인지 확인한다.** 안 그러면 또 같은 데서 넘어진다.
with zipfile.ZipFile(OUT) as z:
    names = z.namelist()
bad = [x for x in names if '\\' in x]
if bad:
    print('🔴 역슬래시가 남았다:', bad[:3]); sys.exit(1)
if 'index.html' not in names:
    print('🔴 index.html 이 zip 뿌리에 없다 — itch 가 못 찾는다'); sys.exit(1)

mb = os.path.getsize(OUT) / 1024.0 / 1024.0
print('파일 %d개 · %.1f MB → %s' % (n, mb, OUT))
print('뿌리에 index.html 있음 · 경로 구분자 전부 슬래시')
