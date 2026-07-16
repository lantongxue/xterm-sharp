#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import process from 'node:process';

const requestPath = process.argv[2];
if (!requestPath) {
  console.error('Usage: node tools/compare-reference.mjs <request.json>');
  process.exit(2);
}

const request = readFileSync(requestPath, 'utf8');
const reference = spawnSync(process.execPath, ['tools/reference-runner.mjs'], {
  cwd: process.cwd(), input: request, encoding: 'utf8'
});
if (reference.status !== 0) {
  console.error(reference.stderr || 'The xterm.js reference runner failed. Build the pinned headless package first.');
  process.exit(reference.status ?? 1);
}

const candidate = spawnSync('dotnet', [
  'run', '--project', 'tools/XtermSharp.Conformance/XtermSharp.Conformance.csproj', '--no-launch-profile'
], { cwd: process.cwd(), input: request, encoding: 'utf8' });
if (candidate.status !== 0) {
  console.error(candidate.stderr || candidate.stdout);
  process.exit(candidate.status ?? 1);
}

const expected = JSON.parse(reference.stdout);
const actual = JSON.parse(candidate.stdout);
if (JSON.stringify(expected) === JSON.stringify(actual)) {
  console.log('MATCH');
  process.exit(0);
}

console.error('MISMATCH');
console.error(JSON.stringify({ expected, actual }, null, 2));
process.exit(1);

