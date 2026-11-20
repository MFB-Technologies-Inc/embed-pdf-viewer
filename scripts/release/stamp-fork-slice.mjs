#!/usr/bin/env node
// Stamps a fork slice that CI built elsewhere into buildFork's cache, so
// buildFork returns it instead of building the fork again:
//
//   node scripts/release/stamp-fork-slice.mjs <target> <staging-dir>
//
// Run from the viewer checkout. Writes $PERF_WORK_DIR/fork/<runtime-src
// gitlink>/<target>.json once staging-dir's BUILD-METADATA.json names that
// commit and target.
//
// Exit 64: bad arguments. Exit 65: the staging dir isn't a slice of this
// target at the gitlink (or has no metadata or library).

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { FORK_RECIPE, FORK_TARGETS } from '../../perf-compare/scripts/build-arm.mjs';
import { workDir } from '../../perf-compare/scripts/materialize.mjs';

const RUNTIME_SRC = 'packages/engine/runtime/runtime-src';

const [target, stagingArg] = process.argv.slice(2);
if (!FORK_TARGETS.includes(target) || !stagingArg) {
  console.error(`usage: stamp-fork-slice.mjs <${FORK_TARGETS.join('|')}> <staging-dir>`);
  process.exit(64);
}
const staging = path.resolve(stagingArg);

const git = (args, cwd) => execFileSync('git', args, { cwd, encoding: 'utf8' }).trim();
const top = git(['rev-parse', '--show-toplevel'], process.cwd());
const sha = git(['ls-tree', 'HEAD', RUNTIME_SRC], top).split(/\s+/)[2];
if (!sha) {
  console.error(`no ${RUNTIME_SRC} gitlink at HEAD in ${top}`);
  process.exit(64);
}

let metadata;
let libSha256;
try {
  metadata = JSON.parse(readFileSync(path.join(staging, 'BUILD-METADATA.json'), 'utf8'));
  libSha256 = createHash('sha256')
    .update(readFileSync(path.join(staging, 'lib/libembedpdf.a')))
    .digest('hex');
} catch (error) {
  console.error(`${staging} isn't a fork slice: ${error.message}`);
  process.exit(65);
}
if (metadata.sha !== sha || metadata.target !== target) {
  console.error(
    `${staging} is ${metadata.target} at ${metadata.sha}, ` +
      `but ${RUNTIME_SRC} pins ${sha} and the target is ${target}`,
  );
  process.exit(65);
}

const root = path.join(workDir(), 'fork', sha);
mkdirSync(root, { recursive: true });
const stampFile = path.join(root, `${target}.json`);
const stamp = { recipe: FORK_RECIPE, sha, target, seeded: false, staging, libSha256 };
writeFileSync(stampFile, JSON.stringify(stamp, null, 2));
console.log(`stamped ${stampFile}`);
