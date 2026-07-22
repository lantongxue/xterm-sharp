#!/usr/bin/env node
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { isDeepStrictEqual } from 'node:util';
import process from 'node:process';
import { Terminal } from '../xterm.js/headless/lib-headless/xterm-headless.mjs';

const fixtureRoot = new URL('../xterm.js/test/fixtures/escape_sequence_files/', import.meta.url);
const candidateDll = new URL(
  '../tools/XtermSharp.Conformance/bin/Debug/net10.0/XtermSharp.Conformance.dll',
  import.meta.url);

if (!existsSync(candidateDll)) {
  console.error('Build tools/XtermSharp.Conformance before running fixture comparison.');
  process.exit(2);
}

const fixtures = readdirSync(fixtureRoot)
  .filter(file => file.endsWith('.in'))
  .sort();
const failures = [];

for (const file of fixtures) {
  const bytes = readFileSync(new URL(file, fixtureRoot));
  const request = {
    options: { cols: 80, rows: 25, scrollback: 1000, convertEol: true },
    operations: [{ type: 'writeBytes', data: [...bytes] }]
  };
  const expected = await referenceSnapshot(bytes, request.options);
  const candidate = spawnSync('dotnet', [fileURLToPath(candidateDll)], {
    cwd: process.cwd(),
    input: JSON.stringify(request),
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024
  });
  if (candidate.status !== 0) {
    failures.push(`${file}: candidate failed: ${candidate.error?.message || candidate.stderr || candidate.stdout}`);
    continue;
  }

  const actual = JSON.parse(candidate.stdout);
  const expectedViewport = fixtureProjection(expected);
  const actualViewport = fixtureProjection(actual);
  if (!isDeepStrictEqual(expectedViewport, actualViewport)) {
    failures.push(`${file}: ${firstDifference(expectedViewport, actualViewport)}`);
  }
}

if (failures.length > 0) {
  console.error(`MISMATCH ${failures.length}/${fixtures.length}`);
  console.error(failures.join('\n'));
  process.exit(1);
}

console.log(`MATCH ${fixtures.length}/${fixtures.length} escape-sequence fixtures`);

async function referenceSnapshot(bytes, options) {
  const terminal = new Terminal({ allowProposedApi: true, ...options });
  const events = [];
  terminal.onBell(() => events.push({ type: 'bell' }));
  terminal.onData(data => events.push({ type: 'data', data }));
  terminal.onCursorMove(() => events.push({ type: 'cursor' }));
  terminal.onLineFeed(() => events.push({ type: 'lineFeed' }));
  terminal.onResize(({ cols, rows }) => events.push({ type: 'resize', cols, rows }));
  terminal.onScroll(viewportY => events.push({ type: 'scroll', viewportY }));
  terminal.onTitleChange(title => events.push({ type: 'title', title }));

  await new Promise(resolve => terminal.write(bytes, resolve));
  const result = {
    columns: terminal.cols,
    rows: terminal.rows,
    activeBuffer: terminal.buffer.active.type,
    modes: JSON.parse(JSON.stringify(terminal.modes)),
    normal: snapshotBuffer(terminal.buffer.normal),
    alternate: snapshotBuffer(terminal.buffer.alternate),
    events
  };
  terminal.dispose();
  return result;
}

function color(cell, foreground) {
  if (foreground ? cell.isFgRGB() : cell.isBgRGB()) return 'rgb';
  if (foreground ? cell.isFgPalette() : cell.isBgPalette()) return 'palette';
  return 'default';
}

function snapshotBuffer(buffer) {
  const lines = [];
  const reusable = buffer.getNullCell();
  for (let y = 0; y < buffer.length; y++) {
    const line = buffer.getLine(y);
    const cells = [];
    if (line) {
      for (let x = 0; x < line.length; x++) {
        const cell = line.getCell(x, reusable);
        cells.push({
          text: cell?.getChars() ?? '',
          codePoint: cell?.getCode() ?? 0,
          width: cell?.getWidth() ?? 1,
          foregroundMode: cell ? color(cell, true) : 'default',
          foreground: cell?.getFgColor() ?? -1,
          backgroundMode: cell ? color(cell, false) : 'default',
          background: cell?.getBgColor() ?? -1,
          bold: !!cell?.isBold(),
          dim: !!cell?.isDim(),
          italic: !!cell?.isItalic(),
          underline: !!cell?.isUnderline(),
          blink: !!cell?.isBlink(),
          inverse: !!cell?.isInverse(),
          invisible: !!cell?.isInvisible(),
          strikethrough: !!cell?.isStrikethrough(),
          overline: !!cell?.isOverline()
        });
      }
    }
    lines.push({ wrapped: !!line?.isWrapped, cells });
  }
  return {
    kind: buffer.type,
    cursorX: buffer.cursorX,
    cursorY: buffer.cursorY,
    viewportY: buffer.viewportY,
    baseY: buffer.baseY,
    lines
  };
}

function fixtureProjection(snapshot) {
  const buffer = snapshot[snapshot.activeBuffer];
  return buffer.lines
    .slice(buffer.viewportY, buffer.viewportY + snapshot.rows)
    .map(line => line.cells
      .filter(cell => cell.width !== 0)
      .map(cell => cell.text || ' ')
      .join('')
      .replace(/ +$/, ''));
}

function firstDifference(expected, actual, path = '$') {
  if (Object.is(expected, actual)) return '';
  if (typeof expected !== typeof actual || expected === null || actual === null) {
    return `${path}: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)}`;
  }
  if (typeof expected !== 'object') {
    return `${path}: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)}`;
  }
  if (Array.isArray(expected) || Array.isArray(actual)) {
    if (!Array.isArray(expected) || !Array.isArray(actual) || expected.length !== actual.length) {
      return `${path}.length: expected ${expected.length}, actual ${actual.length}`;
    }
    for (let index = 0; index < expected.length; index++) {
      const difference = firstDifference(expected[index], actual[index], `${path}[${index}]`);
      if (difference) return difference;
    }
    return '';
  }
  const keys = new Set([...Object.keys(expected), ...Object.keys(actual)]);
  for (const key of keys) {
    if (!(key in expected) || !(key in actual)) {
      return `${path}.${key}: missing property`;
    }
    const difference = firstDifference(expected[key], actual[key], `${path}.${key}`);
    if (difference) return difference;
  }
  return '';
}
