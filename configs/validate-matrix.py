import re
line = [l for l in open('.github/workflows/game-review.yml', encoding='utf-8') if l.strip().startswith('shard:')][0]
expr = line[line.find('fromJSON(') + 9:line.rfind(')')]
toks = []
i = 0
while i < len(expr):
    c = expr[i]
    if c == ' ':
        i += 1
        continue
    if c == "'":
        j = expr.find("'", i + 1)
        toks.append(('str', expr[i + 1:j]))
        i = j + 1
        continue
    if expr.startswith('&&', i):
        toks.append(('and',)); i += 2; continue
    if expr.startswith('||', i):
        toks.append(('or',)); i += 2; continue
    if expr.startswith('==', i):
        toks.append(('eq',)); i += 2; continue
    if c == '(':
        toks.append(('op',)); i += 1; continue
    if c == ')':
        toks.append(('cp',)); i += 1; continue
    m = re.match(r'[A-Za-z0-9_.]+', expr[i:])
    if m:
        toks.append(('id', m.group(0))); i += len(m.group(0)); continue
    raise SystemExit('bad char ' + repr(c) + ' at ' + str(i) + ' ctx: ' + repr(expr[max(0,i-60):i+20]))
pos = [0]
def pe():
    return toks[pos[0]] if pos[0] < len(toks) else ('eof',)
def px():
    t = toks[pos[0]]; pos[0] += 1; return t
def val():
    t = px()
    if t[0] == 'str': return ('str', t[1])
    if t[0] == 'id':
        if t[1] == 'inputs.depth': return ('k', 'depth')
        if t[1] == 'inputs.case_set': return ('k', 'case_set')
        if t[1] == 'inputs.begin_progress_slide': return ('c', 'slide')
        if t[1] == 'inputs.begin_progress_cell': return ('c', 'cell')
        if t[1] == 'inputs.begin_progress_phase': return ('c', 'phase')
        if t[1] == 'inputs.begin_progress_column': return ('c', 'column')
        return ('r', t[1])
    raise SystemExit('bad val ' + str(t))
def at():
    t = pe()
    if t[0] == 'op':
        px(); v = ore(); assert px()[0] == 'cp'; return v
    a = val()
    if a[0] == 'k' and pe()[0] == 'eq':
        px(); b = val(); assert b[0] == 'str'
        return ('eq', a[1], b[1])
    if a[0] == 'str': return ('lit', a[1])
    if a[0] == 'c': return ('c', a[1])
    if a[0] == 'r': return ('r', a[1])
    raise SystemExit('bad at ' + str(a))
def an():
    v = at()
    while pe()[0] == 'and':
        px(); v = ('an', v, at())
    return v
def ore():
    v = an()
    while pe()[0] == 'or':
        px(); v = ('or', v, an())
    return v
def ev(n, env):
    k = n[0]
    if k == 'eq': return env.get(n[1]) == n[2]
    if k == 'str': return n[1]
    if k == 'lit': return n[1]
    if k == 'c': return env.get(n[1])
    if k == 'r': return env.get(n[1], '')
    if k == 'an':
        l = ev(n[1], env)
        return ev(n[2], env) if l else l
    if k == 'or':
        l = ev(n[1], env)
        return l if l else ev(n[2], env)
    raise SystemExit('bad node ' + str(n))
t = ore()
assert pe()[0] == 'eof'
tests = [
    (('driving_validation', 'wetnight_matrix_dry'), '[0,8]'),
    (('driving_validation', 'wetnight_matrix_wet'), '[0,8]'),
    (('driving_validation', None), '[0]'),
    (('fleet_evidence', None), '[0,1,2,3,4,5]'),
    (('night_evidence', None), '[0]'),
    (('car_evidence', None), '[0]'),
    (('input_shard', None), '[0]'),
]
ok = True
for (d, cs), want in tests:
    env = {'depth': d}
    if cs: env['case_set'] = cs
    got = ev(t, env)
    flag = 'OK' if got == want else 'MISMATCH'
    if got != want: ok = False
    print(f'{d}/{cs or "-"} -> {got!r} (want {want!r}) {flag}')
print('ALL OK' if ok else 'FAILURES PRESENT')
