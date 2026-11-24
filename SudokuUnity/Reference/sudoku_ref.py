# Python implementation of the Sudoku solver and the difficulty model (same algorithms as the C# engine).
# Handy for experiments: rate_steps(), solve_human(), gen_directed().
import itertools, random, time
from collections import deque

# ---------------- geometry ----------------
ROWS = [[r * 9 + c for c in range(9)] for r in range(9)]
COLS = [[r * 9 + c for r in range(9)] for c in range(9)]
BOXES = [[(br * 3 + r) * 9 + bc * 3 + c for r in range(3) for c in range(3)]
         for br in range(3) for bc in range(3)]
UNITS = ROWS + COLS + BOXES


def box_of(i):
    return (i // 27) * 3 + (i % 9) // 3


UNITS_OF = [[i // 9, 9 + i % 9, 18 + box_of(i)] for i in range(81)]
PEERS = []
for _i in range(81):
    _s = set()
    for _u in UNITS_OF[_i]:
        _s.update(UNITS[_u])
    _s.discard(_i)
    PEERS.append(sorted(_s))
PEERSET = [set(p) for p in PEERS]


def sees(a, b):
    return a != b and b in PEERSET[a]


BIT = [0] + [1 << (d - 1) for d in range(1, 10)]


def pc(m):
    return bin(m).count('1')


def digits_of(m):
    return [d for d in range(1, 10) if m & BIT[d]]


# ---------------- model from the paper ----------------
VE, E, ME, H, EX, XT = range(6)
LEVELS = ['VeryEasy', 'Easy', 'Medium', 'Hard', 'Expert', 'Extreme']
# name: (first M, increment I, minimal level)
TABLE = [
    ('Single', 1, 1, VE), ('NakedPair', 10, 5, VE), ('HiddenPair', 12, 5, E),
    ('BoxLine', 15, 7, E), ('NakedTriple', 25, 10, ME), ('HiddenTriple', 30, 10, ME),
    ('XYWing', 180, 30, H), ('XYZWing', 220, 35, H), ('XWing', 200, 30, H),
    ('Swordfish', 300, 50, EX), ('Jellyfish', 400, 70, EX),
    ('SimpleColoring', 150, 40, H), ('AdvancedColoring', 250, 60, EX),
    ('XYChainShort', 180, 30, H), ('XYChainLong', 250, 50, EX),
    ('UniqueRectangle', 200, 40, H), ('ALS', 300, 50, EX),
    ('ForcingShort', 250, 50, EX), ('ForcingLong', 350, 80, EX),
    ('Nishio', 500, 150, XT), ('BruteForce', 800, 200, XT)]
INFO = {t[0]: t[1:] for t in TABLE}
SHORT = 6  # chain length / propagation rounds treated as "short"


def level_from_score(s):
    if s < 50: return VE
    if s < 200: return E
    if s < 500: return ME
    if s < 1000: return H
    if s < 2000: return EX
    return XT


def rate_steps(steps):
    counts = {}
    for s in steps:
        counts[s.tech] = counts.get(s.tech, 0) + 1
    total = 0
    minlvl = VE
    for t, n in counts.items():
        M, I, ml = INFO[t]
        total += M + (n - 1) * I
        minlvl = max(minlvl, ml)
    by_score = level_from_score(total)
    return total, by_score, minlvl, max(by_score, minlvl), counts


# ---------------- state ----------------
class State:
    def __init__(self):
        self.val = [0] * 81
        self.cand = [0] * 81

    @staticmethod
    def from_grid(grid):
        s = State()
        s.val = list(grid)
        for i in range(81):
            if s.val[i] == 0:
                m = 0x1FF
                for p in PEERS[i]:
                    if s.val[p]:
                        m &= ~BIT[s.val[p]]
                s.cand[i] = m
        return s

    def copy(self):
        s = State()
        s.val = self.val[:]
        s.cand = self.cand[:]
        return s

    def place(self, i, d):
        self.val[i] = d
        self.cand[i] = 0
        nb = ~BIT[d]
        for p in PEERS[i]:
            self.cand[p] &= nb

    def solved(self):
        return 0 not in self.val


class Step:
    def __init__(self, tech, places, elims, cells, variant=''):
        self.tech, self.places, self.elims, self.cells, self.variant = tech, places, elims, cells, variant


def apply_step(st, step):
    changed = False
    for i, d in step.elims:
        if st.cand[i] & BIT[d]:
            st.cand[i] &= ~BIT[d]
            changed = True
    for i, d in step.places:
        st.place(i, d)
        changed = True
    if not changed:
        raise RuntimeError('no-op step ' + step.tech)


# ---------------- techniques ----------------
def find_single(st):
    for i in range(81):
        if st.val[i] == 0:
            m = st.cand[i]
            if m and (m & (m - 1)) == 0:
                return Step('Single', [(i, m.bit_length())], [], [i], 'Naked')
    for u in UNITS:
        for d in range(1, 10):
            pos = [i for i in u if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if len(pos) == 1:
                return Step('Single', [(pos[0], d)], [], [pos[0]], 'Hidden')
    return None


def find_naked(st, k):
    name = 'NakedPair' if k == 2 else 'NakedTriple'
    for u in UNITS:
        cells = [i for i in u if st.val[i] == 0 and 2 <= pc(st.cand[i]) <= k]
        for combo in itertools.combinations(cells, k):
            m = 0
            for i in combo:
                m |= st.cand[i]
            if pc(m) != k:
                continue
            el = []
            for j in u:
                if st.val[j] == 0 and j not in combo:
                    for d in digits_of(st.cand[j] & m):
                        el.append((j, d))
            if el:
                return Step(name, [], el, list(combo))
    return None


def find_hidden(st, k):
    name = 'HiddenPair' if k == 2 else 'HiddenTriple'
    for u in UNITS:
        pos = {}
        for d in range(1, 10):
            p = [i for i in u if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if 2 <= len(p) <= k:
                pos[d] = p
        for combo in itertools.combinations(sorted(pos), k):
            cells = set()
            for d in combo:
                cells.update(pos[d])
            if len(cells) != k:
                continue
            dm = 0
            for d in combo:
                dm |= BIT[d]
            el = []
            for i in sorted(cells):
                for d in digits_of(st.cand[i] & ~dm):
                    el.append((i, d))
            if el:
                return Step(name, [], el, sorted(cells))
    return None


def find_intersection(st):
    for b in range(9):
        bu = BOXES[b]
        bs = set(bu)
        for d in range(1, 10):
            cells = [i for i in bu if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if len(cells) < 2:
                continue
            rs = {i // 9 for i in cells}
            cs = {i % 9 for i in cells}
            if len(rs) == 1:
                r = next(iter(rs))
                el = [(j, d) for j in ROWS[r] if j not in bs and st.val[j] == 0 and st.cand[j] & BIT[d]]
                if el: return Step('BoxLine', [], el, cells, 'Pointing')
            if len(cs) == 1:
                c = next(iter(cs))
                el = [(j, d) for j in COLS[c] if j not in bs and st.val[j] == 0 and st.cand[j] & BIT[d]]
                if el: return Step('BoxLine', [], el, cells, 'Pointing')
    for line in ROWS + COLS:
        ls = set(line)
        for d in range(1, 10):
            cells = [i for i in line if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if len(cells) < 2:
                continue
            bxs = {box_of(i) for i in cells}
            if len(bxs) == 1:
                b = next(iter(bxs))
                el = [(j, d) for j in BOXES[b] if j not in ls and st.val[j] == 0 and st.cand[j] & BIT[d]]
                if el: return Step('BoxLine', [], el, cells, 'BoxLine')
    return None


def find_fish(st, k):
    name = {2: 'XWing', 3: 'Swordfish', 4: 'Jellyfish'}[k]
    for d in range(1, 10):
        for orient in (0, 1):
            base = ROWS if orient == 0 else COLS
            lines = []
            for li in range(9):
                cells = [i for i in base[li] if st.val[i] == 0 and st.cand[i] & BIT[d]]
                if 2 <= len(cells) <= k:
                    lines.append((li, cells, [(i % 9) if orient == 0 else (i // 9) for i in cells]))
            for combo in itertools.combinations(lines, k):
                cov = set()
                for _, _, cv in combo:
                    cov.update(cv)
                if len(cov) != k:
                    continue
                bset = {li for li, _, _ in combo}
                el = []
                for cv in sorted(cov):
                    line = COLS[cv] if orient == 0 else ROWS[cv]
                    for j in line:
                        bl = (j // 9) if orient == 0 else (j % 9)
                        if bl not in bset and st.val[j] == 0 and st.cand[j] & BIT[d]:
                            el.append((j, d))
                if el:
                    cells = []
                    for _, cs, _ in combo:
                        cells += cs
                    return Step(name, [], el, cells)
    return None


def find_xy_wing(st):
    bi = [i for i in range(81) if st.val[i] == 0 and pc(st.cand[i]) == 2]
    for p in bi:
        pm = st.cand[p]
        wings = [w for w in PEERS[p] if st.val[w] == 0 and pc(st.cand[w]) == 2 and pc(st.cand[w] & pm) == 1]
        for a, b in itertools.combinations(wings, 2):
            ma, mb = st.cand[a], st.cand[b]
            if (ma & pm) == (mb & pm):
                continue
            z = ma & mb
            if pc(z) != 1 or (z & pm):
                continue
            zd = z.bit_length()
            el = [(j, zd) for j in range(81)
                  if st.val[j] == 0 and (st.cand[j] & z) and j not in (a, b, p) and sees(a, j) and sees(b, j)]
            if el:
                return Step('XYWing', [], el, [p, a, b])
    return None


def find_xyz_wing(st):
    for p in range(81):
        if st.val[p] == 0 and pc(st.cand[p]) == 3:
            pm = st.cand[p]
            wings = [w for w in PEERS[p] if st.val[w] == 0 and pc(st.cand[w]) == 2 and (st.cand[w] & ~pm) == 0]
            for a, b in itertools.combinations(wings, 2):
                ma, mb = st.cand[a], st.cand[b]
                if ma == mb or (ma | mb) != pm:
                    continue
                z = ma & mb
                if pc(z) != 1:
                    continue
                zd = z.bit_length()
                el = [(j, zd) for j in range(81)
                      if st.val[j] == 0 and (st.cand[j] & z) and j not in (p, a, b)
                      and sees(p, j) and sees(a, j) and sees(b, j)]
                if el:
                    return Step('XYZWing', [], el, [p, a, b])
    return None


def find_coloring(st):
    for d in range(1, 10):
        adj = {}
        for u in UNITS:
            pos = [i for i in u if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if len(pos) == 2:
                a, b = pos
                adj.setdefault(a, set()).add(b)
                adj.setdefault(b, set()).add(a)
        seen = set()
        for start in sorted(adj):
            if start in seen:
                continue
            color = {start: 0}
            stack = [start]
            while stack:
                x = stack.pop()
                for y in adj[x]:
                    if y not in color:
                        color[y] = 1 - color[x]
                        stack.append(y)
            seen.update(color)
            comp = [sorted(c for c, v in color.items() if v == k) for k in (0, 1)]
            # rule 2 (wrap): two cells of one colour see each other -> that colour is false
            for k in (0, 1):
                if any(sees(a, b) for a, b in itertools.combinations(comp[k], 2)):
                    return Step('SimpleColoring', [], [(c, d) for c in comp[k]], comp[0] + comp[1], 'Wrap')
            # rule 4 (trap): a cell seeing both colours cannot hold d
            el = []
            for j in range(81):
                if st.val[j] == 0 and (st.cand[j] & BIT[d]) and j not in color:
                    if any(sees(j, c) for c in comp[0]) and any(sees(j, c) for c in comp[1]):
                        el.append((j, d))
            if el:
                return Step('SimpleColoring', [], el, comp[0] + comp[1], 'Trap')
    return None


def find_ur(st):
    for r1, r2 in itertools.combinations(range(9), 2):
        for c1, c2 in itertools.combinations(range(9), 2):
            cells = [r1 * 9 + c1, r1 * 9 + c2, r2 * 9 + c1, r2 * 9 + c2]
            if len({box_of(i) for i in cells}) != 2:
                continue
            if any(st.val[i] for i in cells):
                continue
            masks = [st.cand[i] for i in cells]
            common = masks[0] & masks[1] & masks[2] & masks[3]
            for a, b in itertools.combinations(digits_of(common), 2):
                pm = BIT[a] | BIT[b]
                floor = [cells[k] for k in range(4) if masks[k] == pm]
                roof = [cells[k] for k in range(4) if masks[k] != pm]
                if len(floor) == 3:
                    return Step('UniqueRectangle', [], [(roof[0], a), (roof[0], b)], cells, 'Type1')
                if len(floor) == 2:
                    r1c, r2c = roof
                    x1 = st.cand[r1c] & ~pm
                    x2 = st.cand[r2c] & ~pm
                    if x1 == x2 and pc(x1) == 1:
                        z = x1.bit_length()
                        el = [(j, z) for j in range(81) if j not in cells and st.val[j] == 0
                              and st.cand[j] & x1 and sees(r1c, j) and sees(r2c, j)]
                        if el:
                            return Step('UniqueRectangle', [], el, cells, 'Type2')
                    for x, y in ((a, b), (b, a)):
                        for u in set(UNITS_OF[r1c]) & set(UNITS_OF[r2c]):
                            pos = [i for i in UNITS[u] if st.val[i] == 0 and st.cand[i] & BIT[x]]
                            if sorted(pos) == sorted([r1c, r2c]):
                                return Step('UniqueRectangle', [], [(r1c, y), (r2c, y)], cells, 'Type4')
    return None


def find_xy_chain(st, lo, hi):
    bi = [i for i in range(81) if st.val[i] == 0 and pc(st.cand[i]) == 2]
    biset = set(bi)
    best = None
    for a in bi:
        ma = st.cand[a]
        for x in digits_of(ma):
            y = (ma & ~BIT[x]).bit_length()
            start = (a, y)
            parent = {start: None}
            depth = {start: 1}
            q = deque([start])
            while q:
                cur = q.popleft()
                cell, out = cur
                for nb in PEERS[cell]:
                    if nb in biset and (st.cand[nb] & BIT[out]):
                        nout = (st.cand[nb] & ~BIT[out]).bit_length()
                        ns = (nb, nout)
                        if ns in parent:
                            continue
                        parent[ns] = cur
                        depth[ns] = depth[cur] + 1
                        q.append(ns)
                        n = depth[ns]
                        if nout == x and lo <= n <= hi and (best is None or n < best[0]):
                            path = []
                            s = ns
                            while s is not None:
                                path.append(s[0])
                                s = parent[s]
                            if len(set(path)) != len(path):
                                continue
                            el = [(j, x) for j in range(81) if st.val[j] == 0 and st.cand[j] & BIT[x]
                                  and j not in path and sees(a, j) and sees(nb, j)]
                            if el:
                                best = (n, el, path[::-1])
    if best:
        n, el, path = best
        return Step('XYChainShort' if n <= SHORT else 'XYChainLong', [], el, path)
    return None


# ---------------- propagation (singles only) ----------------
def propagate_round(st):
    """returns 'contra', 'stuck' or 'prog'. Applies all current singles at once."""
    pl = {}
    for i in range(81):
        if st.val[i] == 0:
            m = st.cand[i]
            if m == 0:
                return 'contra'
            if (m & (m - 1)) == 0:
                pl[i] = m.bit_length()
    for u in UNITS:
        for d in range(1, 10):
            pos = [i for i in u if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if len(pos) == 0:
                if not any(st.val[i] == d for i in u):
                    return 'contra'
            elif len(pos) == 1:
                i = pos[0]
                if i in pl and pl[i] != d:
                    return 'contra'
                pl[i] = d
    if not pl:
        return 'stuck'
    for i, d in pl.items():
        for j in PEERS[i]:
            if pl.get(j) == d:
                return 'contra'
    for i, d in pl.items():
        st.place(i, d)
    return 'prog'


def find_forcing(st, max_rounds):
    sources = []
    for i in range(81):
        if st.val[i] == 0 and 2 <= pc(st.cand[i]) <= 3:
            sources.append([(i, d) for d in digits_of(st.cand[i])])
    for u in UNITS:
        for d in range(1, 10):
            pos = [i for i in u if st.val[i] == 0 and st.cand[i] & BIT[d]]
            if 2 <= len(pos) <= 3:
                sources.append([(i, d) for i in pos])
    best = None
    for src in sources:
        branches = []
        for i, d in src:
            s = st.copy()
            s.place(i, d)
            branches.append(s)
        res = None
        for r in range(1, max_rounds + 1):
            stuck = 0
            bad = False
            for s in branches:
                o = propagate_round(s)
                if o == 'contra':
                    bad = True
                    break
                if o == 'stuck':
                    stuck += 1
            if bad:
                break
            el = []
            for j in range(81):
                if st.val[j] == 0:
                    union = 0
                    for s in branches:
                        union |= s.cand[j] if s.val[j] == 0 else BIT[s.val[j]]
                    for d in digits_of(st.cand[j] & ~union):
                        el.append((j, d))
            if el:
                res = (r, el)
                break
            if stuck == len(branches):
                break
        if res and (best is None or res[0] < best[0]):
            best = (res[0], res[1], [i for i, _ in src])
    if best:
        r, el, cells = best
        return Step('ForcingShort' if r <= SHORT else 'ForcingLong', [], el, cells)
    return None


def find_nishio(st):
    best = None
    for i in range(81):
        if st.val[i] == 0 and pc(st.cand[i]) >= 2:
            for d in digits_of(st.cand[i]):
                s = st.copy()
                s.place(i, d)
                rounds = 0
                contra = False
                while True:
                    o = propagate_round(s)
                    if o == 'contra':
                        contra = True
                        break
                    if o == 'stuck':
                        break
                    rounds += 1
                if contra and (best is None or (rounds, i, d) < best):
                    best = (rounds, i, d)
    if best:
        return Step('Nishio', [], [(best[1], best[2])], [best[1]])
    return None


# ---------------- solver ----------------
FINDERS = {
    'Single': find_single,
    'NakedPair': lambda st: find_naked(st, 2),
    'HiddenPair': lambda st: find_hidden(st, 2),
    'BoxLine': find_intersection,
    'NakedTriple': lambda st: find_naked(st, 3),
    'HiddenTriple': lambda st: find_hidden(st, 3),
    'SimpleColoring': find_coloring,
    'XYWing': find_xy_wing,
    'XYChainShort': lambda st: find_xy_chain(st, 4, SHORT),
    'XWing': lambda st: find_fish(st, 2),
    'UniqueRectangle': find_ur,
    'XYZWing': find_xyz_wing,
    'XYChainLong': lambda st: find_xy_chain(st, SHORT + 1, 99),
    'ForcingShort': lambda st: find_forcing(st, SHORT),
    'Swordfish': lambda st: find_fish(st, 3),
    'ForcingLong': lambda st: find_forcing(st, 99),
    'Jellyfish': lambda st: find_fish(st, 4),
    'Nishio': find_nishio,
}
# easiest first: sort by first-application weight M (stable -> table order breaks ties)
ORDER = [t[0] for t in sorted(TABLE, key=lambda t: t[1]) if t[0] in FINDERS]


def next_step(st, solution, unique=True):
    for name in ORDER:
        if name == 'UniqueRectangle' and not unique:
            continue
        step = FINDERS[name](st)
        if step:
            return step
    best = None
    for i in range(81):
        if st.val[i] == 0:
            n = pc(st.cand[i])
            if best is None or n < best[0]:
                best = (n, i)
    i = best[1]
    return Step('BruteForce', [(i, solution[i])], [], [i])


def solve_human(grid, solution, unique=True, check=False):
    st = State.from_grid(grid)
    steps = []
    while not st.solved():
        step = next_step(st, solution, unique)
        if check and step.tech != 'BruteForce':
            for i, d in step.places:
                assert solution[i] == d, ('bad placement', step.tech, i, d)
            for i, d in step.elims:
                assert solution[i] != d, ('bad elimination', step.tech, step.variant, i, d)
        apply_step(st, step)
        steps.append(step)
    return steps


# ---------------- backtracking / generator ----------------
def solve_count(grid, limit=2, rng=None):
    rows = [0] * 9; cols = [0] * 9; bxs = [0] * 9
    g = list(grid)
    for i, v in enumerate(g):
        if v:
            b = BIT[v]; r, c = divmod(i, 9); x = box_of(i)
            if rows[r] & b or cols[c] & b or bxs[x] & b:
                return 0, None
            rows[r] |= b; cols[c] |= b; bxs[x] |= b
    state = {'n': 0, 'first': None}

    def rec():
        best = -1; bm = 0; bc = 10
        for i in range(81):
            if g[i] == 0:
                r = i // 9; c = i % 9
                m = ~(rows[r] | cols[c] | bxs[box_of(i)]) & 0x1FF
                n = pc(m)
                if n < bc:
                    bc = n; best = i; bm = m
                    if n <= 1:
                        break
        if best < 0:
            state['n'] += 1
            if state['first'] is None:
                state['first'] = g[:]
            return
        if bc == 0:
            return
        r = best // 9; c = best % 9; x = box_of(best)
        ds = digits_of(bm)
        if rng:
            rng.shuffle(ds)
        for d in ds:
            b = BIT[d]
            g[best] = d; rows[r] |= b; cols[c] |= b; bxs[x] |= b
            rec()
            g[best] = 0; rows[r] &= ~b; cols[c] &= ~b; bxs[x] &= ~b
            if state['n'] >= limit:
                return

    rec()
    return state['n'], state['first']


def random_solution(rng):
    return solve_count([0] * 81, 1, rng)[1]


def dig(sol, rng, goal):
    puzzle = list(sol)
    order = list(range(81))
    rng.shuffle(order)
    clues = 81
    for i in order:
        if clues <= goal:
            break
        v = puzzle[i]
        puzzle[i] = 0
        if solve_count(puzzle, 2)[0] != 1:
            puzzle[i] = v
        else:
            clues -= 1
    return puzzle


def parse(s):
    return [0 if ch in '.0' else int(ch) for ch in s]


# ---------------- capped solving and level-directed generator (mirrors PuzzleGenerator.cs) ----------------
def solve_capped(grid, solution, cap, unique=True):
    """Solve with techniques not above level `cap` and without leaving the score range of `cap`; None otherwise."""
    st = State.from_grid(grid)
    steps = []
    counts = {}
    while not st.solved():
        step = None
        for name in ORDER:
            if INFO[name][2] > cap:
                continue
            if name == 'UniqueRectangle' and not unique:
                continue
            step = FINDERS[name](st)
            if step:
                break
        if step is None:
            return None
        counts[step.tech] = counts.get(step.tech, 0) + 1
        score = sum(INFO[t][0] + (n - 1) * INFO[t][1] for t, n in counts.items())
        if level_from_score(score) > cap:
            return None
        apply_step(st, step)
        steps.append(step)
    return steps


def gen_directed(target, rng, sample=1):
    """sample=1: plain random digging; sample>1: pick, among `sample` candidates, the removal with the highest score S."""
    sol = random_solution(rng)
    p = list(sol)
    remaining = list(range(81))
    rng.shuffle(remaining)
    while remaining:
        best = None
        dead = []
        for c in remaining[:sample]:
            v = p[c]
            p[c] = 0
            val = None
            if solve_count(p, 2)[0] == 1:
                steps = solve_capped(p, sol, target)
                if steps is not None:
                    val = rate_steps(steps)[0]
            p[c] = v
            if val is None:
                dead.append(c)
            elif best is None or val > best[0]:
                best = (val, c)
        for c in dead:
            remaining.remove(c)
        if best:
            p[best[1]] = 0
            remaining.remove(best[1])
    return p, sol
