import { readFileSync, writeFileSync } from 'fs';

const [manifestPath, entryJson, releaseType] = process.argv.slice(2);

const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
if (!manifest[releaseType]) manifest[releaseType] = [];

manifest[releaseType].unshift(JSON.parse(entryJson));

if (releaseType === 'Dev' && manifest.Dev.length > 30)
  manifest.Dev = manifest.Dev.slice(0, 30);

writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
