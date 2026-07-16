#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const EXPECTED_COMMIT = 'b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7';
const EXPECTED_VERSION = '6.0.0';
const EXPECTED_TESTS = 1361;
const EXPECTED_TEST_FILES = 37;
const EXPECTED_EXCLUDED = 54;

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '..');
const upstreamRoot = path.join(repositoryRoot, 'xterm.js');
const defaultOutput = path.join(repositoryRoot, 'tests', 'upstream-tests.json');
const defaultPortMap = path.join(repositoryRoot, 'tests', 'upstream-port-map.json');

const options = parseArguments(process.argv.slice(2));
verifyUpstreamBaseline();

const mochaResult = options.input
  ? JSON.parse(fs.readFileSync(options.input, 'utf8'))
  : runMochaDryRun();
const existingManifest = readJsonIfPresent(options.output);
const portMap = readJsonIfPresent(options.portMap) ?? { schemaVersion: 1, entries: [] };
const manifest = buildManifest(mochaResult, existingManifest, portMap);
const serialized = `${JSON.stringify(manifest, null, 2)}\n`;

if (options.check) {
  if (!existingManifest) {
    fail(`Cannot check ${relativeToRoot(options.output)} because it does not exist.`);
  }
  const current = fs.readFileSync(options.output, 'utf8');
  if (current !== serialized) {
    fail(`${relativeToRoot(options.output)} is stale. Run node tools/generate-upstream-tests.mjs.`);
  }
  console.log(`Verified ${relativeToRoot(options.output)} (${manifest.counts.discovered} tests).`);
} else {
  fs.writeFileSync(options.output, serialized);
  console.log(
    `Generated ${relativeToRoot(options.output)}: ` +
    `${manifest.counts.discovered} discovered, ${manifest.counts.excludedRenderer} renderer exclusions, ` +
    `${manifest.counts.required} required.`
  );
}

function parseArguments(args) {
  const parsed = {
    check: false,
    input: undefined,
    output: defaultOutput,
    portMap: defaultPortMap
  };

  for (let index = 0; index < args.length; index++) {
    const argument = args[index];
    switch (argument) {
      case '--check':
        parsed.check = true;
        break;
      case '--input':
        parsed.input = requireValue(args, ++index, argument);
        break;
      case '--output':
        parsed.output = path.resolve(requireValue(args, ++index, argument));
        break;
      case '--port-map':
        parsed.portMap = path.resolve(requireValue(args, ++index, argument));
        break;
      default:
        fail(`Unknown argument: ${argument}`);
    }
  }

  return parsed;
}

function requireValue(args, index, argument) {
  if (index >= args.length) {
    fail(`${argument} requires a path.`);
  }
  return path.resolve(args[index]);
}

function verifyUpstreamBaseline() {
  const packageJson = JSON.parse(fs.readFileSync(path.join(upstreamRoot, 'package.json'), 'utf8'));
  if (packageJson.version !== EXPECTED_VERSION) {
    fail(`Expected xterm.js ${EXPECTED_VERSION}, found ${packageJson.version}.`);
  }

  const actualCommit = readGitHead(resolveGitDirectory(path.join(upstreamRoot, '.git')));
  if (actualCommit !== EXPECTED_COMMIT) {
    fail(`Expected xterm.js ${EXPECTED_COMMIT}, found ${actualCommit}.`);
  }
}

function resolveGitDirectory(dotGitPath) {
  if (fs.statSync(dotGitPath).isDirectory()) {
    return dotGitPath;
  }

  const marker = 'gitdir:';
  const contents = fs.readFileSync(dotGitPath, 'utf8').trim();
  if (!contents.startsWith(marker)) {
    fail(`Cannot resolve Git directory from ${relativeToRoot(dotGitPath)}.`);
  }
  return path.resolve(path.dirname(dotGitPath), contents.slice(marker.length).trim());
}

function readGitHead(gitDirectory) {
  const head = fs.readFileSync(path.join(gitDirectory, 'HEAD'), 'utf8').trim();
  if (!head.startsWith('ref: ')) {
    return head;
  }

  const reference = head.slice('ref: '.length);
  const looseReference = path.join(gitDirectory, ...reference.split('/'));
  if (fs.existsSync(looseReference)) {
    return fs.readFileSync(looseReference, 'utf8').trim();
  }

  const packedReferences = fs.readFileSync(path.join(gitDirectory, 'packed-refs'), 'utf8');
  const match = packedReferences
    .split(/\r?\n/u)
    .find(line => !line.startsWith('#') && !line.startsWith('^') && line.endsWith(` ${reference}`));
  if (!match) {
    fail(`Cannot resolve xterm.js Git reference ${reference}.`);
  }
  return match.slice(0, match.indexOf(' '));
}

function runMochaDryRun() {
  const mocha = path.join(upstreamRoot, 'node_modules', 'mocha', 'bin', 'mocha.js');
  const outRoot = path.join(upstreamRoot, 'out-esbuild');
  if (!fs.existsSync(mocha) || !fs.existsSync(outRoot)) {
    fail(
      'xterm.js build output is missing. Run `npm ci`, `npm run build`, and `npm run esbuild` ' +
      'inside xterm.js first.'
    );
  }

  const result = spawnSync(
    process.execPath,
    [mocha, 'out-esbuild/{common,headless}/**/*.test.js', '--dry-run', '--reporter', 'json'],
    {
      cwd: upstreamRoot,
      encoding: 'utf8',
      env: { ...process.env, NODE_PATH: outRoot },
      maxBuffer: 64 * 1024 * 1024
    }
  );

  if (result.error) {
    fail(`Could not start Mocha: ${result.error.message}`);
  }
  if (result.status !== 0) {
    fail(`Mocha dry-run failed (${result.status}):\n${result.stderr}`);
  }
  return JSON.parse(result.stdout);
}

function buildManifest(mochaResult, existingManifest, portMap) {
  if (mochaResult?.stats?.tests !== EXPECTED_TESTS || mochaResult?.tests?.length !== EXPECTED_TESTS) {
    fail(
      `Expected ${EXPECTED_TESTS} expanded Mocha tests, found ` +
      `${mochaResult?.stats?.tests ?? 'unknown'} stats/${mochaResult?.tests?.length ?? 'unknown'} records.`
    );
  }

  const testFileCount = countCompiledTestFiles(path.join(upstreamRoot, 'out-esbuild'));
  if (testFileCount !== EXPECTED_TEST_FILES) {
    fail(`Expected ${EXPECTED_TEST_FILES} common/headless test files, found ${testFileCount}.`);
  }

  const normalized = mochaResult.tests.map(test => ({
    file: normalizeSourceFile(test.file),
    fullTitle: test.fullTitle
  }));
  // Do not sort: the pinned Mocha discovery order is the canonical ID order already used by C# ports.

  const existingByIdentity = new Map(
    (existingManifest?.tests ?? []).map(test => [identityKey(test), test])
  );
  const occurrenceByTitle = new Map();
  const tests = normalized.map((test, index) => {
    const titleKey = `${test.file}\u0000${test.fullTitle}`;
    const occurrence = (occurrenceByTitle.get(titleKey) ?? 0) + 1;
    occurrenceByTitle.set(titleKey, occurrence);

    const id = `XTJS-${String(index + 1).padStart(4, '0')}`;
    const excluded = isRendererExclusion(test.file, test.fullTitle);
    const identity = { ...test, occurrence };
    const previous = existingByIdentity.get(identityKey(identity));
    const preserved = !excluded && isPortableStatus(previous?.status) ? previous : undefined;

    return {
      id,
      ...identity,
      area: excluded ? 'Renderer' : classifyArea(test.file),
      status: excluded ? 'Excluded.Renderer' : preserved?.status ?? 'Pending',
      csharpTest: preserved?.csharpTest ?? null,
      difference: preserved?.difference ?? null,
      exclusionReason: excluded
        ? 'Front-end rendering support; outside the headless C# scope.'
        : null
    };
  });

  applyPortMap(tests, portMap);
  validateGeneratedTests(tests);

  const statusCount = status => tests.filter(test => test.status === status).length;
  return {
    schemaVersion: 1,
    upstream: {
      repository: 'https://github.com/xtermjs/xterm.js',
      commit: EXPECTED_COMMIT,
      version: EXPECTED_VERSION
    },
    generator: 'tools/generate-upstream-tests.mjs',
    counts: {
      discoveredFiles: EXPECTED_TEST_FILES,
      discovered: tests.length,
      excludedRenderer: statusCount('Excluded.Renderer'),
      required: tests.length - statusCount('Excluded.Renderer'),
      pending: statusCount('Pending'),
      ported: statusCount('Ported'),
      architectureEquivalent: statusCount('ArchitectureEquivalent')
    },
    tests
  };
}

function countCompiledTestFiles(root) {
  let count = 0;
  for (const area of ['common', 'headless']) {
    const pending = [path.join(root, area)];
    while (pending.length > 0) {
      const directory = pending.pop();
      for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
        const entryPath = path.join(directory, entry.name);
        if (entry.isDirectory()) {
          pending.push(entryPath);
        } else if (entry.isFile() && entry.name.endsWith('.test.js')) {
          count++;
        }
      }
    }
  }
  return count;
}

function normalizeSourceFile(file) {
  const normalized = path.resolve(file).replaceAll('\\', '/');
  const marker = '/out-esbuild/';
  const markerIndex = normalized.lastIndexOf(marker);
  if (markerIndex < 0 || !normalized.endsWith('.test.js')) {
    fail(`Unexpected Mocha test path: ${file}`);
  }
  return `src/${normalized.slice(markerIndex + marker.length, -'.js'.length)}.ts`;
}

function identityKey(test) {
  return `${test.file}\u0000${test.fullTitle}\u0000${test.occurrence}`;
}

function isPortableStatus(status) {
  return status === 'Pending' || status === 'Ported' || status === 'ArchitectureEquivalent';
}

function isRendererExclusion(file, fullTitle) {
  if (
    file === 'src/common/Color.test.ts' ||
    file === 'src/common/MultiKeyMap.test.ts' ||
    file === 'src/common/SortedList.test.ts' ||
    file === 'src/common/services/CoreService.test.ts' ||
    file === 'src/common/services/DecorationService.test.ts'
  ) {
    return true;
  }
  return file === 'src/common/services/OptionsService.test.ts' && fullTitle.includes('fontWeight');
}

function classifyArea(file) {
  if (
    file === 'src/common/CircularList.test.ts' ||
    file === 'src/common/Event.test.ts' ||
    file === 'src/common/StringBuilder.test.ts'
  ) {
    return 'Utilities';
  }
  if (
    file === 'src/common/input/TextDecoder.test.ts' ||
    file === 'src/common/input/UnicodeV6.test.ts' ||
    file === 'src/common/input/WriteBuffer.test.ts' ||
    file === 'src/common/input/XParseColor.test.ts'
  ) {
    return 'Decoder/Unicode/WriteBuffer/XParseColor';
  }
  if (file === 'src/common/WindowsMode.test.ts') {
    return 'WindowsMode';
  }
  if (file.startsWith('src/common/parser/')) {
    return 'Parser';
  }
  if (file.startsWith('src/common/buffer/')) {
    return 'Buffer/Line/Cell/Reflow';
  }
  if (file.startsWith('src/common/services/')) {
    return 'Services';
  }
  if (file === 'src/common/public/AddonManager.test.ts') {
    return 'Addon';
  }
  if (file === 'src/common/InputHandler.test.ts') {
    return 'InputHandler';
  }
  if (
    file === 'src/common/input/Keyboard.test.ts' ||
    file === 'src/common/input/KittyKeyboard.test.ts' ||
    file === 'src/common/input/Win32InputMode.test.ts'
  ) {
    return 'Keyboard/Kitty/Win32';
  }
  if (file === 'src/headless/public/Terminal.test.ts') {
    return 'Headless Terminal';
  }
  fail(`No test area mapping for ${file}.`);
}

function applyPortMap(tests, portMap) {
  if (portMap.schemaVersion !== 1 || !Array.isArray(portMap.entries)) {
    fail(`${relativeToRoot(options.portMap)} must have schemaVersion 1 and an entries array.`);
  }

  const byId = new Map(tests.map(test => [test.id, test]));
  const seen = new Set();
  for (const entry of portMap.entries) {
    if (seen.has(entry.id)) {
      fail(`Duplicate port-map entry for ${entry.id}.`);
    }
    seen.add(entry.id);

    const test = byId.get(entry.id);
    if (!test) {
      fail(`Unknown port-map ID ${entry.id}.`);
    }
    if (test.status === 'Excluded.Renderer') {
      fail(`Port-map ID ${entry.id} is an excluded renderer test.`);
    }
    if (entry.status !== 'Ported' && entry.status !== 'ArchitectureEquivalent') {
      fail(`Port-map ${entry.id} status must be Ported or ArchitectureEquivalent.`);
    }
    if (typeof entry.csharpTest !== 'string' || entry.csharpTest.length === 0) {
      fail(`Port-map ${entry.id} must name csharpTest.`);
    }
    if (entry.status === 'ArchitectureEquivalent' &&
        (typeof entry.difference !== 'string' || entry.difference.length === 0)) {
      fail(`Port-map ${entry.id} must explain its architecture difference.`);
    }

    test.status = entry.status;
    test.csharpTest = entry.csharpTest;
    test.difference = entry.difference ?? null;
  }
}

function validateGeneratedTests(tests) {
  const exclusions = tests.filter(test => test.status === 'Excluded.Renderer');
  if (exclusions.length !== EXPECTED_EXCLUDED) {
    fail(`Expected ${EXPECTED_EXCLUDED} renderer exclusions, found ${exclusions.length}.`);
  }

  const expectedAreaCounts = new Map([
    ['Utilities', 43],
    ['Decoder/Unicode/WriteBuffer/XParseColor', 315],
    ['WindowsMode', 3],
    ['Parser', 244],
    ['Buffer/Line/Cell/Reflow', 138],
    ['Services', 35],
    ['Addon', 2],
    ['InputHandler', 192],
    ['Keyboard/Kitty/Win32', 290],
    ['Headless Terminal', 45],
    ['Renderer', 54]
  ]);
  for (const [area, expected] of expectedAreaCounts) {
    const actual = tests.filter(test => test.area === area).length;
    if (actual !== expected) {
      fail(`Expected ${expected} ${area} tests, found ${actual}.`);
    }
  }

  for (const test of tests) {
    if ((test.status === 'Ported' || test.status === 'ArchitectureEquivalent') && !test.csharpTest) {
      fail(`${test.id} is ${test.status} but does not name csharpTest.`);
    }
    if (test.status === 'ArchitectureEquivalent' && !test.difference) {
      fail(`${test.id} is ArchitectureEquivalent but has no difference explanation.`);
    }
  }
}

function readJsonIfPresent(file) {
  return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : undefined;
}

function relativeToRoot(file) {
  return path.relative(repositoryRoot, file).replaceAll('\\', '/');
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
