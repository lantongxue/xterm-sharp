#!/usr/bin/env node

import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import process from 'node:process';

const scenarioPath = process.argv[2] ?? 'tools/reflow-scenarios.json';
const document = JSON.parse(readFileSync(scenarioPath, 'utf8'));
const scenarios = document.scenarios ?? [];
const names = new Set();
let matched = 0;

for (const scenario of scenarios) {
  if (!scenario.name || !scenario.request || names.has(scenario.name)) {
    console.error(`Invalid or duplicate reflow scenario '${scenario.name ?? ''}'.`);
    process.exit(2);
  }
  names.add(scenario.name);

  const request = JSON.stringify(scenario.request);
  const expected = run(process.execPath, ['tools/reference-runner.mjs'], request, 'xterm.js');
  const actual = run('dotnet', [
    'run',
    '--project',
    'tools/XtermSharp.Conformance/XtermSharp.Conformance.csproj',
    '--no-build',
    '--no-launch-profile'
  ], request, 'XtermSharp');
  delete expected.events;
  delete actual.events;

  if (JSON.stringify(expected) !== JSON.stringify(actual)) {
    console.error(`MISMATCH ${scenario.name}`);
    console.error(JSON.stringify(firstDifferences(expected, actual), null, 2));
    process.exit(1);
  }
  matched++;
}

console.log(`MATCH ${matched}/${scenarios.length} complex reflow scenarios`);

function run(command, args, input, name) {
  const result = spawnSync(command, args, {
    cwd: process.cwd(),
    input,
    encoding: 'utf8',
    maxBuffer: 20 * 1024 * 1024
  });
  if (result.status !== 0) {
    console.error(result.stderr || result.stdout || `${name} runner failed.`);
    process.exit(result.status ?? 1);
  }
  return JSON.parse(result.stdout);
}

function firstDifferences(expected, actual) {
  const differences = [];
  compare(expected, actual, '$', differences);
  return differences;
}

function compare(expected, actual, path, differences) {
  if (differences.length >= 30 || Object.is(expected, actual)) return;
  if (expected === null || actual === null ||
      typeof expected !== 'object' || typeof actual !== 'object') {
    differences.push({ path, expected, actual });
    return;
  }
  if (Array.isArray(expected) !== Array.isArray(actual)) {
    differences.push({ path, expected, actual });
    return;
  }
  if (Array.isArray(expected)) {
    if (expected.length !== actual.length) {
      differences.push({ path: `${path}.length`, expected: expected.length, actual: actual.length });
    }
    for (let index = 0; index < Math.min(expected.length, actual.length); index++) {
      compare(expected[index], actual[index], `${path}[${index}]`, differences);
    }
    return;
  }
  const keys = new Set([...Object.keys(expected), ...Object.keys(actual)]);
  for (const key of keys) {
    compare(expected[key], actual[key], `${path}.${key}`, differences);
  }
}
