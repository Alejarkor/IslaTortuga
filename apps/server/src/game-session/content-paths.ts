import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';

export function resolveContentPacksRoot() {
  const configuredRoot = process.env.CONTENT_PACKS_ROOT?.trim();
  if (configuredRoot) {
    return resolve(configuredRoot);
  }

  let current = process.cwd();

  while (true) {
    const candidate = join(current, 'content-packs');
    if (existsSync(candidate)) {
      return candidate;
    }

    const parent = resolve(current, '..');
    if (parent === current) {
      break;
    }

    current = parent;
  }

  return resolve(process.cwd(), 'content-packs');
}

export function resolveContentIndexPath() {
  return join(resolveContentPacksRoot(), 'index.json');
}
