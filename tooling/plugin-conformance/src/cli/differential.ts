// `pnpm run` entry: runs the check unconditionally, so no way of naming
// this file can load it without running it. `as-library` comes first: it
// lets the check module load (see `library-guard.ts`).
import '../as-library';

import { main } from '../checks/differential';

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 2;
});
