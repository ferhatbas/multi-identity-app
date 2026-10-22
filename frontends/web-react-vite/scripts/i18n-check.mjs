import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const root = 'src';
const dir = join(root, 'i18n', 'locales');

function* files(folder) {
  for (const name of readdirSync(folder)) {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) yield* files(path);
    else if (/\.tsx?$/.test(name)) yield path;
  }
}

const unescape = text => text.replace(/\\(.)/g, '$1');
const call = /\bt\(\s*(?:'((?:[^'\\]|\\.)*)'|"((?:[^"\\]|\\.)*)")/g;

const used = new Set();
for (const file of files(root)) {
  const source = readFileSync(file, 'utf8');
  for (const m of source.matchAll(call)) used.add(unescape(m[1] ?? m[2]));
}

const sorted = list => [...list].sort((a, b) => a.localeCompare(b, 'en'));
writeFileSync(join(dir, 'en.json'), JSON.stringify(Object.fromEntries(sorted(used).map(k => [k, k])), null, 2) + '\n');

let missingTotal = 0;
for (const name of readdirSync(dir).filter(n => n.endsWith('.json') && n !== 'en.json')) {
  const dictionary = JSON.parse(readFileSync(join(dir, name), 'utf8'));
  const missing = sorted(used).filter(k => !(k in dictionary));
  const unused = Object.keys(dictionary).filter(k => !used.has(k));
  missingTotal += missing.length;
  console.log(`${name}: ${Object.keys(dictionary).length} entries, ${missing.length} missing, ${unused.length} unused`);
  if (missing.length) console.log(missing.map(k => `  + ${k}`).join('\n'));
  if (unused.length) console.log(unused.map(k => `  - ${k}`).join('\n'));
}
console.log(`en.json: ${used.size} sentences`);
if (process.argv.includes('--strict') && missingTotal) process.exit(1);
