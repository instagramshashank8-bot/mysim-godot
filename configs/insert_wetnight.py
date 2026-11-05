import subprocess
# 1) restore the committed (clean) workflow line
subprocess.run(['git', 'checkout', '--', '.github/workflows/game-review.yml'], check=True)
p = '.github/workflows/game-review.yml'
t = open(p, encoding='utf-8').read()
assert 'wetnight_matrix_dry' not in t.split('shard:')[1].split('\n')[0] if 'shard:' in t else True
# 2) insert before the CHIRON group's opener (the OUTER group) - a fixed, known anchor
anchor = " || (inputs.depth == 'chiron_wing_rebuild'"
assert anchor in t, 'chiron anchor missing'
entry = " || inputs.depth == 'driving_validation' && (inputs.case_set == 'wetnight_matrix_dry' || inputs.case_set == 'wetnight_matrix_wet') && '[0,8]'"
t = t.replace(anchor, entry + anchor, 1)
open(p, 'w', encoding='utf-8').write(t)
print('inserted before the chiron group')
