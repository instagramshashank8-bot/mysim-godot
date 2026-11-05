import re
p = '.github/workflows/game-review.yml'
t = open(p, encoding='utf-8').read()
bad = '|| || inputs.depth'
if bad in t:
    t = t.replace(bad, '|| inputs.depth')
    open(p, 'w', encoding='utf-8').write(t)
    print('double-or removed')
else:
    print('no double-or present')
