/**
 * Makes running a `src/checks/` module directly fail instead of doing
 * nothing.
 *
 * A check module exports `main()` and never calls it; its `src/cli/` entry
 * does. Run as the process entry (`tsx src/checks/replay-check.ts`), it would
 * load, check nothing and exit 0. Comparing `process.argv[1]` with
 * `import.meta.url` cannot tell those cases apart under every spelling of the
 * path, so this does not look at paths at all. Whoever means to use a check
 * as a library imports `src/as-library.ts` before the check: every
 * `src/cli/` entry and every test does. ES modules evaluate their imports in
 * order, so the mark is set before any check module's body runs. A check
 * module calls `refuseDirectRun` from its body; without the mark, the
 * process exits 2 and, once nothing else runs, names the pnpm script to use.
 */

const MARK = Symbol.for('@embedpdf/tooling-plugin-conformance/as-library');

/** Says the check modules imported after this are used as libraries. */
export function markAsLibrary(): void {
  (globalThis as Record<symbol, unknown>)[MARK] = true;
}

let refused: { module: string; script: string } | null = null;

/** Fail the process unless `src/as-library.ts` was imported first. `module`
 *  is the check's path in the package, `script` its `pnpm run` script. */
export function refuseDirectRun(module: string, script: string): void {
  if ((globalThis as Record<symbol, unknown>)[MARK]) return;
  process.exitCode = 2;
  // A check that imports another evaluates after it, so the last one
  // refused is the module that was run.
  if (refused === null) {
    process.once('beforeExit', () => {
      console.error(
        `${refused!.module} is a library and checks nothing when run directly: run \`pnpm --filter @embedpdf/tooling-plugin-conformance run ${refused!.script}\``,
      );
    });
  }
  refused = { module, script };
}
