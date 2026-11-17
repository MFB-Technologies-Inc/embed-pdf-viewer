/** Stops a check on an argument it does not take, so a typo such as
 *  `--chekc` fails with exit 2 instead of running the check's default mode.
 *  `flags` take no value; `valued` take the next argument. A bare `--`, which
 *  `pnpm run <script> -- ...` passes through, is skipped. */
export function rejectUnknownArgs(
  argv: readonly string[],
  flags: readonly string[],
  valued: readonly string[] = [],
): void {
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--' || flags.includes(arg)) continue;
    if (valued.includes(arg)) {
      i++;
      continue;
    }
    console.error(`unknown argument ${arg}`);
    process.exit(2);
  }
}
