#!/usr/bin/env node
// Builds one PDFium fork slice at the runtime-src gitlink, for a CI job that
// uploads it:
//
//   node scripts/release/build-fork-slice.mjs <target> <out-dir>
//
// Leaves the slice's staging dir at <out-dir>/staging and its archive,
// libembedpdf-runtime-<target>-<sha7>.tar.gz, in <out-dir>.
//
// The Linux, Android and wasm targets are built by the fork's own scripts in
// the runtime-src checkout, which must be checked out at the gitlink; a wasm
// build also needs the emsdk its emsdk-version.sh pins already active in
// runtime-src/third_party/emsdk, where a CI cache can restore it. The iOS and
// macOS targets go through buildFork, so the slice matches what the publish
// script would build. wasm32-eh is one of buildFork's targets too, but takes
// the fork-script path here: buildFork would install its emsdk uncached.
//
// Exit 64: bad arguments.

import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildFork, FORK_TARGETS } from '../../perf-compare/scripts/build-arm.mjs';
import { workDir } from '../../perf-compare/scripts/materialize.mjs';

const FORK_SCRIPT_TARGETS = ['linux-arm64', 'android-arm64', 'android-x64', 'wasm32', 'wasm32-eh'];

const [target, outArg] = process.argv.slice(2);
const targets = [...new Set([...FORK_TARGETS, ...FORK_SCRIPT_TARGETS])];
if (!targets.includes(target) || !outArg) {
  if (target && !targets.includes(target)) console.error(`unknown target ${target}`);
  console.error(`usage: build-fork-slice.mjs <${targets.join('|')}> <out-dir>`);
  process.exit(64);
}
const out = path.resolve(outArg);

const repo = fileURLToPath(new URL('../..', import.meta.url));
const src = path.join(repo, 'packages/engine/runtime/runtime-src');

function sh(cmd, cwd, env = {}, stdio = 'inherit') {
  console.log(`  $ ${cmd}`);
  return execFileSync('bash', ['-euo', 'pipefail', '-c', cmd], {
    cwd,
    stdio,
    encoding: 'utf8',
    env: { ...process.env, ...env },
  });
}

// The one archive package-target.sh wrote for this target into `dir`.
function archiveIn(dir) {
  const names = readdirSync(dir).filter(
    (name) => name.startsWith(`libembedpdf-runtime-${target}-`) && name.endsWith('.tar.gz'),
  );
  if (names.length !== 1) {
    throw new Error(`expected one ${target} archive in ${dir}, found ${names.length}`);
  }
  return path.join(dir, names[0]);
}

mkdirSync(out, { recursive: true });

let staging;
let archive;
if (FORK_SCRIPT_TARGETS.includes(target)) {
  // The same depot_tools buildFork uses, which a CI cache can restore.
  const depotTools = path.join(workDir(), 'depot_tools');
  if (!existsSync(path.join(depotTools, 'python3_bin_reldir.txt'))) {
    if (!existsSync(depotTools)) {
      mkdirSync(workDir(), { recursive: true });
      sh(
        `git clone --depth 1 https://chromium.googlesource.com/chromium/tools/depot_tools.git ${depotTools}`,
        workDir(),
      );
    }
    // On PATH so the bootstrap's gsutil step finds depot_tools' own luci-auth.
    sh('./ensure_bootstrap', depotTools, { PATH: `${depotTools}:${process.env.PATH}` });
  }
  const env = {
    PATH: `${depotTools}:${process.env.PATH}`,
    DEPOT_TOOLS_UPDATE: '0',
    PDF_RUNTIME_TARGET_OS_LIST: 'linux android emscripten',
    PDF_RUNTIME_ARTIFACT_DIR: out,
  };
  sh(`scripts/embedpdf-runtime/build-target.sh ${target}`, src, env);
  // package-target.sh prints the archive's path last.
  const packaged = sh(`scripts/embedpdf-runtime/package-target.sh ${target}`, src, env, [
    'ignore',
    'pipe',
    'inherit',
  ]);
  process.stdout.write(packaged);
  archive = packaged.trim().split('\n').at(-1);
  staging = path.join(src, 'out/embedpdf-runtime-staging', target);
} else {
  const stamp = buildFork(repo, target);
  staging = stamp.staging;
  archive = archiveIn(path.join(workDir(), 'fork', stamp.sha, 'artifacts'));
}

cpSync(staging, path.join(out, 'staging'), { recursive: true });
if (path.dirname(archive) !== out) cpSync(archive, path.join(out, path.basename(archive)));
console.log(`${target} slice in ${out}`);
