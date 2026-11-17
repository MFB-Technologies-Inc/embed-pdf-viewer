// `pnpm run` entry: runs the check unconditionally, so no way of naming
// this file can load it without running it.
import { main } from '../rust-coverage';

await main();
