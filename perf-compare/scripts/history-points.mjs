// Fills in history/points.json and gives every point an arm.
//
//   node scripts/history-points.mjs
//
// Milestones are written into points.json by hand: a series, a commit and a
// note. This adds the interval points of each series that has
// `intervalDays` (lib/history.mjs `selectIntervalPoints`), resolves every
// commit to its full SHA and UTC commit time, and names the arm that builds
// each point: one arms.json already has, or a new `hist-<sha9>` worktree and
// arm added there. A point with `skip` keeps its entry and gets no arm.
// Running it again with nothing new changes neither file.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { armFor, selectIntervalPoints } from '../lib/history.mjs';

const pointsFile = fileURLToPath(new URL('../history/points.json', import.meta.url));
const armsFile = fileURLToPath(new URL('../arms.json', import.meta.url));
const repo = fileURLToPath(new URL('../..', import.meta.url));
const git = (args) => execFileSync('git', args, { cwd: repo, encoding: 'utf8', maxBuffer: 64 * 2 ** 20 }).trim();
const utcDate = (iso) => new Date(iso).toISOString().slice(0, 10);

const history = JSON.parse(readFileSync(pointsFile, 'utf8'));
const config = JSON.parse(readFileSync(armsFile, 'utf8'));
const before = [JSON.stringify(history), JSON.stringify(config)];

// `date` is the commit time in UTC, to the second: several points can share a
// day, and the page draws them in commit order.
const stamp = (p) => {
  if (!history.series[p.series]) throw new Error(`point ${p.sha}: unknown series ${p.series}`);
  p.sha = git(['rev-parse', `${p.sha}^{commit}`]);
  p.date = new Date(git(['show', '-s', '--format=%cI', p.sha])).toISOString();
};
history.points.forEach(stamp);

// A series ref may exist only as a remote branch in this checkout, and a
// remote's copy can be behind another's, so the newest tip wins.
const resolve = (ref) => {
  const tips = [];
  for (const name of [ref, ...git(['remote']).split('\n').filter(Boolean).map((r) => `${r}/${ref}`)]) {
    try {
      const sha = git(['rev-parse', '--verify', '--quiet', `${name}^{commit}`]);
      tips.push({ sha, at: Number(git(['show', '-s', '--format=%ct', sha])) });
    } catch {}
  }
  if (!tips.length) throw new Error(`no branch ${ref} here or on a remote`);
  return tips.sort((a, b) => b.at - a.at)[0].sha;
};

for (const [id, s] of Object.entries(history.series)) {
  if (!s.intervalDays) continue;
  const tip = resolve(s.ref);
  const commits = git(['log', '--first-parent', '--format=%H %cI', tip])
    .split('\n')
    .map((line) => ({ sha: line.slice(0, 40), date: line.slice(41) }));
  const added = selectIntervalPoints({
    commits,
    existing: history.points.filter((p) => p.series === id).map((p) => p.date),
    from: s.from,
    to: utcDate(git(['show', '-s', '--format=%cI', tip])),
    days: s.intervalDays,
  });
  for (const { sha, date, target } of added) {
    history.points.push({ series: id, sha, date, why: 'interval', note: `the last commit on or before ${target}` });
    console.log(`${id}: added ${sha.slice(0, 9)} (${date}) for ${target}`);
  }
}

history.points.forEach(stamp);
for (const p of history.points) {
  if (p.skip || p.arm) continue;
  const s = history.series[p.series];
  p.arm = armFor(config, { sha: p.sha, recipe: s.recipe, kernel: s.kernel });
  if (p.arm) continue;
  p.arm = `hist-${p.sha.slice(0, 9)}`;
  config.worktrees[p.arm] = { ref: s.ref, sha: p.sha, recipe: s.recipe };
  config.arms[p.arm] = { worktree: p.arm, kernel: s.kernel, rustReads: false };
  console.log(`${p.series}: new arm ${p.arm}`);
}

const order = Object.keys(history.series);
history.points.sort((a, b) => order.indexOf(a.series) - order.indexOf(b.series) || a.date.localeCompare(b.date) || a.sha.localeCompare(b.sha));
// Key order is fixed so a rerun writes the same bytes.
history.points = history.points.map(({ series, sha, date, why, note, arm, skip, ...rest }) => ({
  series,
  sha,
  date,
  why,
  note,
  ...(arm ? { arm } : {}),
  ...(skip ? { skip } : {}),
  ...rest,
}));

if (JSON.stringify(history) !== before[0]) writeFileSync(pointsFile, JSON.stringify(history, null, 2) + '\n');
if (JSON.stringify(config) !== before[1]) writeFileSync(armsFile, JSON.stringify(config, null, 2) + '\n');
const live = history.points.filter((p) => !p.skip);
console.log(`${history.points.length} points, ${live.length} to measure, ${new Set(live.map((p) => p.arm)).size} arms`);
