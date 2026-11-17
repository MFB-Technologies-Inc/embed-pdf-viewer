/**
 * Whether the module at `moduleUrl` (its `import.meta.url`) is the script
 * node was started with.
 *
 * Node resolves the entry script through symlinks before it loads it, so
 * `import.meta.url` is the real path. `process.argv[1]` is the path as
 * typed, which can go through a symlink, and tsx also accepts it without
 * the file's extension; comparing the two as given would make such a check
 * do nothing and exit 0. So the entry is tried as typed and with the
 * module's extension added, each as a real path.
 */
import { realpathSync } from 'node:fs';
import { extname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export function isMainModule(
  moduleUrl: string,
  entry: string | undefined = process.argv[1],
): boolean {
  if (!entry) return false;
  const modulePath = realpathSync(fileURLToPath(moduleUrl));
  for (const candidate of [entry, `${entry}${extname(modulePath)}`]) {
    try {
      if (realpathSync(resolve(candidate)) === modulePath) return true;
    } catch {
      // Not a file under this spelling; try the next.
    }
  }
  return false;
}
