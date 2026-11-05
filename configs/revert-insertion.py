p = '.github/workflows/game-review.yml'
t = open(p, encoding='utf-8').read()
inserted = " || inputs.depth == 'driving_validation' && (inputs.case_set == 'wetnight_matrix_dry' || inputs.case_set == 'wetnight_matrix_wet') && '[0,8]' || ( inputs.depth == 'night_cutout_review'"
if inserted in t:
    t = t.replace(inserted, " || (inputs.depth == 'night_cutout_review'")
    open(p, 'w', encoding='utf-8').write(t)
    print('bad insertion reverted (space variant)')
else:
    i = t.find('wetnight_matrix_dry', t.find('shard:'))
    print('still not found; matrix-line context:', t[i - 150:i + 80] if i >= 0 else 'NOT FOUND ANYWHERE')
