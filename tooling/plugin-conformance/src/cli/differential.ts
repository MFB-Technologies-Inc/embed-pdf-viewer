// `pnpm run` entry: runs the check unconditionally, so no way of naming
// this file can load it without running it.
import { main } from '../differential';

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 2;
});
