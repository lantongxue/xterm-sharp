#!/usr/bin/env node

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const EXPECTED_COMMIT = 'b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7';
const EXPECTED_VERSION = '6.0.0';
const RANGE_NAMES = ['BMP_COMBINING', 'HIGH_COMBINING', 'BMP_WIDE', 'HIGH_WIDE'];

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '..');
const upstreamRoot = path.join(repositoryRoot, 'xterm.js');
const sourceFile = path.join(upstreamRoot, 'addons', 'addon-unicode11', 'src', 'UnicodeV11.ts');
const defaultOutput = path.join(repositoryRoot, 'src', 'XtermSharp', 'Unicode', 'UnicodeV11Data.cs');

const options = parseArguments(process.argv.slice(2));
verifyUpstreamBaseline();

const source = normalizeLineEndings(fs.readFileSync(sourceFile, 'utf8'));
const ranges = Object.fromEntries(RANGE_NAMES.map(name => [name, parseRanges(source, name)]));
validateRanges(ranges);

const sourceSha256 = crypto.createHash('sha256').update(source).digest('hex').toUpperCase();
const scalarWidths = createScalarWidths(ranges);
const scalarWidthSha256 = crypto.createHash('sha256').update(scalarWidths).digest('hex').toUpperCase();
const generated = renderCSharp(ranges, sourceSha256, scalarWidthSha256, scalarWidths.length);

if (options.check) {
  if (!fs.existsSync(options.output)) {
    fail(`Cannot check ${relativeToRoot(options.output)} because it does not exist.`);
  }
  const current = normalizeLineEndings(fs.readFileSync(options.output, 'utf8'));
  if (current !== generated) {
    fail(`${relativeToRoot(options.output)} is stale. Run node tools/generate-unicode-v11.mjs.`);
  }
  console.log(
    `Verified ${relativeToRoot(options.output)} against xterm.js Unicode 11 ` +
    `(${scalarWidths.length} Unicode scalars).`
  );
} else {
  fs.writeFileSync(options.output, generated);
  console.log(
    `Generated ${relativeToRoot(options.output)} from xterm.js Unicode 11 ` +
    `(${scalarWidths.length} Unicode scalars).`
  );
}

function parseArguments(args) {
  const parsed = { check: false, output: defaultOutput };
  for (let index = 0; index < args.length; index++) {
    const argument = args[index];
    switch (argument) {
      case '--check':
        parsed.check = true;
        break;
      case '--output':
        parsed.output = path.resolve(requireValue(args, ++index, argument));
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
  return args[index];
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

function parseRanges(source, name) {
  const marker = `const ${name} = [`;
  const start = source.indexOf(marker);
  if (start < 0) {
    fail(`Could not find ${name} in ${relativeToRoot(sourceFile)}.`);
  }
  const contentStart = start + marker.length;
  const contentEnd = source.indexOf('\n];', contentStart);
  if (contentEnd < 0) {
    fail(`Could not find the end of ${name} in ${relativeToRoot(sourceFile)}.`);
  }

  const content = source.slice(contentStart, contentEnd);
  const pairPattern = /\[\s*(0x[0-9a-f]+|\d+)\s*,\s*(0x[0-9a-f]+|\d+)\s*\]/giu;
  const result = [];
  for (const match of content.matchAll(pairPattern)) {
    result.push([Number(match[1]), Number(match[2])]);
  }
  if (result.length === 0) {
    fail(`No ranges were parsed for ${name}.`);
  }

  const unmatched = content.replace(pairPattern, '').replace(/[\s,]/gu, '');
  if (unmatched.length !== 0) {
    fail(`Unexpected content while parsing ${name}: ${unmatched.slice(0, 40)}`);
  }
  return result;
}

function validateRanges(ranges) {
  for (const name of RANGE_NAMES) {
    let previousEnd = -1;
    for (const [start, end] of ranges[name]) {
      if (!Number.isInteger(start) || !Number.isInteger(end) || start < 0 || end < start) {
        fail(`Invalid ${name} range ${start}-${end}.`);
      }
      if (start <= previousEnd) {
        fail(`${name} ranges are not strictly ordered at ${start}-${end}.`);
      }
      previousEnd = end;
    }
  }

  assertRangeBounds('BMP_COMBINING', ranges.BMP_COMBINING, 0, 0xFFFF);
  assertRangeBounds('BMP_WIDE', ranges.BMP_WIDE, 0, 0xFFFF);
  assertRangeBounds('HIGH_COMBINING', ranges.HIGH_COMBINING, 0x10000, 0x10FFFF);
  assertRangeBounds('HIGH_WIDE', ranges.HIGH_WIDE, 0x10000, 0x10FFFF);
}

function assertRangeBounds(name, ranges, minimum, maximum) {
  for (const [start, end] of ranges) {
    if (start < minimum || end > maximum) {
      fail(`${name} range ${formatHex(start)}-${formatHex(end)} is outside the expected bounds.`);
    }
  }
}

function createScalarWidths(ranges) {
  const result = Buffer.alloc(0x110000 - 0x800);
  let offset = 0;
  for (let codePoint = 0; codePoint <= 0x10FFFF; codePoint++) {
    if (codePoint >= 0xD800 && codePoint <= 0xDFFF) {
      continue;
    }
    result[offset++] = getWidth(codePoint, ranges);
  }
  if (offset !== result.length) {
    fail(`Expected ${result.length} Unicode scalar widths, generated ${offset}.`);
  }
  return result;
}

function getWidth(codePoint, ranges) {
  if (codePoint < 32 || codePoint >= 0x7F && codePoint < 0xA0) {
    return 0;
  }
  const combining = codePoint < 0x10000 ? ranges.BMP_COMBINING : ranges.HIGH_COMBINING;
  if (isInRanges(codePoint, combining)) {
    return 0;
  }
  const wide = codePoint < 0x10000 ? ranges.BMP_WIDE : ranges.HIGH_WIDE;
  return isInRanges(codePoint, wide) ? 2 : 1;
}

function isInRanges(codePoint, ranges) {
  let low = 0;
  let high = ranges.length - 1;
  while (low <= high) {
    const middle = (low + high) >> 1;
    const [start, end] = ranges[middle];
    if (codePoint < start) {
      high = middle - 1;
    } else if (codePoint > end) {
      low = middle + 1;
    } else {
      return true;
    }
  }
  return false;
}

function renderCSharp(ranges, sourceSha256, scalarWidthSha256, scalarCount) {
  return `// <auto-generated />
// Generated by tools/generate-unicode-v11.mjs from the pinned xterm.js UnicodeV11 provider.
// xterm.js commit: ${EXPECTED_COMMIT}
// Upstream source SHA-256: ${sourceSha256}

namespace XtermSharp.Unicode;

internal static class UnicodeV11Data
{
    internal const int ScalarCount = ${scalarCount};
    internal const string ScalarWidthSha256 = "${scalarWidthSha256}";

${renderRangeField('BmpCombiningRanges', ranges.BMP_COMBINING)}

${renderRangeField('HighCombiningRanges', ranges.HIGH_COMBINING)}

${renderRangeField('BmpWideRanges', ranges.BMP_WIDE)}

${renderRangeField('HighWideRanges', ranges.HIGH_WIDE)}
}
`;
}

function normalizeLineEndings(value) {
  return value.replaceAll('\r\n', '\n');
}

function renderRangeField(fieldName, ranges) {
  const lines = [];
  for (let index = 0; index < ranges.length; index += 4) {
    const values = ranges
      .slice(index, index + 4)
      .flatMap(([start, end]) => [formatHex(start), formatHex(end)]);
    lines.push(`        ${values.join(', ')},`);
  }
  return `    internal static readonly int[] ${fieldName} =
    [
${lines.join('\n')}
    ];`;
}

function formatHex(value) {
  return `0x${value.toString(16).toUpperCase().padStart(4, '0')}`;
}

function relativeToRoot(file) {
  return path.relative(repositoryRoot, file).replaceAll('\\', '/');
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
