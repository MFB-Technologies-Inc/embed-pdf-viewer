// opids.mjs: every scenario case whose i-th apply call carries another opId than the i-th scripted apply answer.
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { zstdDecompressSync } from "node:zlib";
const walk = (d) => readdirSync(d).flatMap((f) => statSync(join(d, f)).isDirectory() ? walk(join(d, f)) : [join(d, f)]);
for (const f of walk("vectors").filter((f) => f.endsWith(".json.zst"))) {
  const v = JSON.parse(zstdDecompressSync(readFileSync(f)).toString());
  for (const c of v.cases ?? []) {
    const docs = c.engine?.documents ?? {}; const cursor = {};
    for (const t of c.trace ?? []) for (const e of t.engine ?? []) {
      if (e.path !== "apply" || !e.doc) continue;
      const i = cursor[e.doc] = (cursor[e.doc] ?? -1) + 1;
      const r = (docs[e.doc]?.responses ?? []).filter((r) => r.path === "apply")[i];
      const want = r?.value?.meta?.opId, got = e.args?.[1]?.opId;
      if (want && got && want !== got) console.log(f, c.name, i, got, want);
    }
  }
}
