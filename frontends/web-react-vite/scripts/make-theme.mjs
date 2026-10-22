import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

const accent = {
  '#ec4899': '#e11d74', '#db2777': '#c9156a', '#be185d': '#a8115a', '#9d174d': '#8a0f4c', '#831843': '#6f0d3f',
  '#f472b6': '#ee5c9d', '#f9a8d4': '#f4a0c4', '#fbcfe8': '#f9c9dc', '#fce7f3': '#fcdfea', '#fdf2f8': '#fdecf3',
};
const light = { '#4b5563': '#2b2f3a', '#f9fafb': '#f3f4f6', '#dfe7ef': '#dcdfe6' };
const dark = { '#111827': '#14161c', '#1f2937': '#1d2027', '#374151': '#2c303a', '#030712': '#0e1014', '#4b5563': '#3a3f4b' };

mkdirSync('public/themes', { recursive: true });
for (const [source, out, neutrals] of [['lara-light-pink', 'fs-light', light], ['lara-dark-pink', 'fs-dark', dark]]) {
  const map = { ...accent, ...neutrals };
  const css = readFileSync(`node_modules/primereact/resources/themes/${source}/theme.css`, 'utf8')
    .replace(/@font-face\s*\{[^}]*\}/g, '')
    .replaceAll('"Inter var", sans-serif', "'Hanken Grotesk', system-ui, sans-serif")
    .replaceAll('"Inter var"', "'Hanken Grotesk'")
    .replace(/#[0-9a-fA-F]{6}\b/g, hex => map[hex.toLowerCase()] ?? hex);
  writeFileSync(`public/themes/${out}.css`, css);
  console.log(out, css.length);
}
