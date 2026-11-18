// Prints crates/plugins/i18n/src/strings/<code>.json from packages/plugin/i18n/src/strings/<code>.ts:
// each module object through JSON.stringify(value, null, 2), with a final newline.
// Usage (repo root): node --import=tsx docs/research/upstream-catchup-2026-11-18/scripts/cu-c5/print-strings.mts [--check]
import { readFileSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
const codes = ["en", "nl", "de", "fr", "es", "zh-cn", "sv", "ja"];
const check = process.argv.includes("--check");
let differ = 0;
for (const code of codes) {
  const mod = await import(pathToFileURL(`packages/plugin/i18n/src/strings/${code}.ts`).href);
  const values = Object.values(mod);
  if (values.length !== 1) throw new Error(`${code}.ts exports ${values.length} values`);
  const text = JSON.stringify(values[0], null, 2) + "\n";
  const path = `crates/plugins/i18n/src/strings/${code}.json`;
  const same = readFileSync(path, "utf8") === text;
  if (!same) differ += 1;
  if (!check && !same) writeFileSync(path, text);
  console.log(`${code}: ${same ? "same" : check ? "differs" : "written"}`);
}
if (check && differ) process.exit(1);
