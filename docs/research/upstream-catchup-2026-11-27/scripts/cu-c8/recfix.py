import textwrap, sys
# 2026-11-27 (CU-C8). usage: recfix.py <docs dir>. Job F re-review Minors 1 and 2 (CU-C8-D2).
base = sys.argv[1]
def wrap(s): return "\n".join(textwrap.wrap(s, 100, break_long_words=False, break_on_hyphens=False)) + "\n"
p = base + "/reports/CU-C7.md"
L = open(p).read().split("\n")
# paragraph lines 3-10 (index 2..9) and 27-32 (26..31)
para1 = " ".join(L[2:10])
para2 = " ".join(L[26:32])
old = "TS row, and `57ad31610` for `rust-coverage:check`."
assert old.replace("TS row, ","") in para2.replace("TS row, ","")
para2 = para2.replace("and `57ad31610` for `rust-coverage:check`.",
  "and, for `rust-coverage:check`, `cf65c1cc2` (code = `2c49e0685`) and `cf65c1cc2` plus the uncommitted record later committed as `57ad31610`.")
L2 = L[:2] + wrap(para1).rstrip("\n").split("\n") + L[10:26] + wrap(para2).rstrip("\n").split("\n") + L[32:]
s = "\n".join(L2)
o50 = "| exit 1, stale, at `2c49e0685`; exit 0 after `57ad31610`:"
assert o50 in s
s = s.replace(o50, "| exit 1, stale, at `cf65c1cc2` (code = `2c49e0685`); exit 0 at `cf65c1cc2` plus the uncommitted record later committed as `57ad31610`:")
o117 = "| `rust-coverage:check`, head | `2c49e0685` |"
assert o117 in s
s = s.replace(o117, "| `rust-coverage:check`, head | `cf65c1cc2` (code = `2c49e0685`) |")
o118 = "| `2c49e0685` tree, then `57ad31610` |"
assert o118 in s
s = s.replace(o118, "| `cf65c1cc2` (code = `2c49e0685`), then `cf65c1cc2` plus the uncommitted record later committed as `57ad31610` |")
open(p, "w").write(s)
d = base + "/decisions.md"
L = open(d).read().split("\n")
i = L.index("No NEW DIVERGENCE. CU-C7 ran every Rust and Node row at `84a64f0d8`, `2c49e0685` and")
j = i
while L[j] != "": j += 1
para = " ".join(L[i:j])
para = para.replace("ran every Rust and Node row at `84a64f0d8`, `2c49e0685` and `57ad31610` (see the report)",
  "ran every Rust and Node row at `84a64f0d8` and `2c49e0685`, and `rust-coverage:check` at `cf65c1cc2` (code = `2c49e0685`) and at `cf65c1cc2` plus the uncommitted record later committed as `57ad31610` (see the report),")
assert "uncommitted" in para
L = L[:i] + wrap(para).rstrip("\n").split("\n") + L[j:]
open(d, "w").write("\n".join(L))
