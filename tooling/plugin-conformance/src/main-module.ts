/**
 * Whether the module at `moduleUrl` (its `import.meta.url`) is the script
 * node was started with.
 *
 * Node resolves the entry script through symlinks before it loads it, so
 * `import.meta.url` is the real path. `process.argv[1]` is the path as
 * typed, which can go through a symlink; comparing the two as given would
 * make a check run through a symlinked path do nothing and exit 0. Both
 * sides are compared as real paths.
 */
import { realpathSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export function isMainModule(
  moduleUrl: string,
  entry: string | undefined = process.argv[1],
): boolean {
  if (!entry) return false;
  let real: string;
  try {
    real = realpathSync(resolve(entry));
  } catch {
    return false;
  }
  return moduleUrl === pathToFileURL(real).href;
}
