// Imported first by every `src/cli/` entry and every test that imports a
// check module: the checks loaded after it are used as libraries, not run
// directly. See `library-guard.ts`.
import { markAsLibrary } from './library-guard';

markAsLibrary();
