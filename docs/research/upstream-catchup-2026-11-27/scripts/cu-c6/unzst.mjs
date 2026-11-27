// CU-C6: decompress vector .zst files. usage: node unzst.mjs <out-dir> <file.zst>...
import { readFileSync, writeFileSync } from "node:fs";
import { zstdDecompressSync } from "node:zlib";
import { basename, join } from "node:path";
const [out, ...files] = process.argv.slice(2);
for (const f of files) writeFileSync(join(out, basename(f).replace(/\.zst$/, "")), zstdDecompressSync(readFileSync(f)));
