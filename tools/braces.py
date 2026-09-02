# -*- coding: utf-8 -*-
"""C# 괄호 짝 검사.  python tools/braces.py

🔴 **컴파일 대신 쓰는 것이 아니다.** 컴파일은 사장님이 하신다 —
   이건 코드를 스크립트로 크게 도려낸 뒤, 사장님께 넘기기 전에
   내가 최소한 확인할 수 있는 하나다. 통과해도 안 돌 수 있다.

문자열·문자·//·/* */·@"" 를 걷어낸 뒤 { ( [ 짝을 센다.
구간을 통째로 잘라내는 편집에서 제일 잘 나는 사고를 잡는다.
"""
import io, glob, os, sys
BS = chr(92)

bad = 0
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'game', 'Assets')
os.chdir(ROOT)
for p in sorted(glob.glob('Scripts/*.cs') + glob.glob('Editor/*.cs')):
    t = io.open(p, encoding='utf-8').read()
    out, i, n = [], 0, len(t)
    st = None
    while i < n:
        c = t[i]; d = t[i:i+2]
        if st is None:
            if d == '//': st = 'line'; i += 2; continue
            if d == '/*': st = 'blk';  i += 2; continue
            if d == '@"': st = 'verb'; i += 2; continue
            if c == '"':  st = 'str';  i += 1; continue
            if c == "'":  st = 'chr';  i += 1; continue
            out.append(c); i += 1; continue
        if st == 'line':
            if c == chr(10): st = None; out.append(c)
            i += 1; continue
        if st == 'blk':
            if d == '*/': st = None; i += 2; continue
            i += 1; continue
        if st == 'verb':
            if d == '""': i += 2; continue
            if c == '"': st = None
            i += 1; continue
        if st in ('str', 'chr'):
            if c == BS: i += 2; continue
            if (st == 'str' and c == '"') or (st == 'chr' and c == "'"): st = None
            i += 1; continue

    s = ''.join(out)
    pairs = {'{': '}', '(': ')', '[': ']'}
    stack, err = [], None
    for k, ch in enumerate(s):
        if ch in pairs: stack.append((ch, k))
        elif ch in '}])':
            if not stack or pairs[stack[-1][0]] != ch:
                err = '%s 짝 안 맞음' % ch; break
            stack.pop()
    if not err and stack:
        err = '안 닫힘: ' + ''.join(x[0] for x in stack[:8])
    if err:
        bad += 1
        print('%-34s %s' % (p, err))
    else:
        print('%-34s 괄호 OK' % p)
sys.exit(1 if bad else 0)
