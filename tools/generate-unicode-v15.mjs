#!/usr/bin/env node

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

const EXPECTED_COMMIT = 'b1aee19ac6d6f4e4d11e4a10a3731b852956bdb7';
const EXPECTED_VERSION = '6.0.0';
const UNICODE_VERSION = '15.0.0';

const GCB = Object.freeze({
  Other: 0,
  CR: 1,
  LF: 2,
  Control: 3,
  Extend: 4,
  Regional_Indicator: 5,
  Prepend: 6,
  SpacingMark: 7,
  L: 8,
  V: 9,
  T: 10,
  LV: 11,
  LVT: 12,
  ZWJ: 13
});

const WIDTH_MASK = 0x03;
const GCB_SHIFT = 2;
const GCB_MASK = 0x3C;
const EXTENDED_PICTOGRAPHIC = 0x40;

const STATE_GCB_MASK = 0x0F;
const STATE_RI_ODD = 0x10;
const STATE_EMOJI_SHIFT = 5;
const STATE_EMOJI_MASK = 0x60;
const EMOJI_NONE = 0;
const EMOJI_EXTEND_SEQUENCE = 1;
const EMOJI_ZWJ_SEQUENCE = 2;

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '..');
const upstreamRoot = path.join(repositoryRoot, 'xterm.js');
const unicodeRoot = path.join(repositoryRoot, 'tools', 'unicode', UNICODE_VERSION);
const upstreamPropertiesFile = path.join(
  upstreamRoot,
  'addons',
  'addon-unicode-graphemes',
  'src',
  'third-party',
  'UnicodeProperties.ts'
);
const graphemePropertyFile = path.join(unicodeRoot, 'GraphemeBreakProperty.txt');
const graphemeTestFile = path.join(unicodeRoot, 'GraphemeBreakTest.txt');
const emojiDataFile = path.join(unicodeRoot, 'emoji-data.txt');
const defaultOutput = path.join(repositoryRoot, 'src', 'XtermSharp', 'Unicode', 'UnicodeV15Data.cs');

const options = parseArguments(process.argv.slice(2));
verifyUpstreamBaseline();
verifyUnicodeInputs();

const upstreamSource = fs.readFileSync(upstreamPropertiesFile, 'utf8');
const graphemeSource = fs.readFileSync(graphemePropertyFile, 'utf8');
const graphemeTestSource = fs.readFileSync(graphemeTestFile, 'utf8');
const emojiSource = fs.readFileSync(emojiDataFile, 'utf8');

const trie = decodeUpstreamTrie(upstreamSource);
const graphemeBreaks = parseGraphemeBreakProperties(graphemeSource);
const extendedPictographic = parseBinaryProperty(emojiSource, 'Extended_Pictographic');
const packedInfo = createPackedInfo(trie, graphemeBreaks, extendedPictographic);
const runs = createRuns(packedInfo);
const scalarPropertySha256 = hashValidScalars(packedInfo);
const graphemeTestCount = validateGraphemeBreakTests(graphemeTestSource, packedInfo);
const generated = renderCSharp(
  runs,
  scalarPropertySha256,
  graphemeTestCount,
  sourceHash(upstreamSource),
  sourceHash(graphemeSource),
  sourceHash(graphemeTestSource),
  sourceHash(emojiSource)
);

if (options.check) {
  if (!fs.existsSync(options.output)) {
    fail(`Cannot check ${relativeToRoot(options.output)} because it does not exist.`);
  }
  const current = fs.readFileSync(options.output, 'utf8');
  if (current !== generated) {
    fail(`${relativeToRoot(options.output)} is stale. Run node tools/generate-unicode-v15.mjs.`);
  }
  console.log(
    `Verified ${relativeToRoot(options.output)} against Unicode ${UNICODE_VERSION} and xterm.js ` +
    `(${runs.starts.length} property runs, ${graphemeTestCount} grapheme tests).`
  );
} else {
  fs.writeFileSync(options.output, generated);
  console.log(
    `Generated ${relativeToRoot(options.output)} from Unicode ${UNICODE_VERSION} and xterm.js ` +
    `(${runs.starts.length} property runs, ${graphemeTestCount} grapheme tests).`
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

function verifyUnicodeInputs() {
  verifyHeader(graphemePropertyFile, '# GraphemeBreakProperty-15.0.0.txt');
  verifyHeader(graphemeTestFile, '# GraphemeBreakTest-15.0.0.txt');
  verifyHeader(emojiDataFile, '# emoji-data.txt');
}

function verifyHeader(file, header) {
  const source = fs.readFileSync(file, 'utf8');
  if (!source.startsWith(header)) {
    fail(`${relativeToRoot(file)} does not start with ${header}.`);
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

function decodeUpstreamTrie(source) {
  const match = /const trieRaw = "([^"]+)";/u.exec(source);
  if (!match) {
    fail(`Could not find trieRaw in ${relativeToRoot(upstreamPropertiesFile)}.`);
  }

  const serialized = Buffer.from(match[1], 'base64');
  const highStart = serialized.readUInt32LE(0);
  const errorValue = serialized.readUInt32LE(4);
  const uncompressedLength = serialized.readUInt32LE(8);
  const firstInflate = zlib.inflateRawSync(serialized.subarray(12));
  const uncompressed = zlib.inflateRawSync(firstInflate);
  if (uncompressed.length !== uncompressedLength || uncompressed.length % 4 !== 0) {
    fail(`Unexpected Unicode trie length ${uncompressed.length}; expected ${uncompressedLength}.`);
  }
  const data = new Uint32Array(
    uncompressed.buffer,
    uncompressed.byteOffset,
    uncompressed.byteLength / Uint32Array.BYTES_PER_ELEMENT
  );
  return { data, highStart, errorValue };
}

function getTrieInfo(trie, codePoint) {
  const SHIFT_1 = 11;
  const SHIFT_2 = 5;
  const SHIFT_1_2 = 6;
  const OMITTED_BMP_INDEX_1_LENGTH = 0x20;
  const INDEX_2_MASK = (1 << SHIFT_1_2) - 1;
  const INDEX_SHIFT = 2;
  const DATA_MASK = (1 << SHIFT_2) - 1;
  const LSCP_INDEX_2_OFFSET = 0x10000 >> SHIFT_2;
  const UTF8_2B_INDEX_2_OFFSET = LSCP_INDEX_2_OFFSET + (0x400 >> SHIFT_2);
  const INDEX_1_OFFSET = UTF8_2B_INDEX_2_OFFSET + (0x800 >> 6);
  const DATA_GRANULARITY = 1 << INDEX_SHIFT;
  const data = trie.data;

  if (codePoint < 0 || codePoint > 0x10FFFF) {
    return trie.errorValue;
  }
  let index;
  if (codePoint < 0xD800 || codePoint > 0xDBFF && codePoint <= 0xFFFF) {
    index = (data[codePoint >> SHIFT_2] << INDEX_SHIFT) + (codePoint & DATA_MASK);
    return data[index];
  }
  if (codePoint <= 0xFFFF) {
    index = (data[LSCP_INDEX_2_OFFSET + ((codePoint - 0xD800) >> SHIFT_2)] << INDEX_SHIFT) +
      (codePoint & DATA_MASK);
    return data[index];
  }
  if (codePoint < trie.highStart) {
    index = data[(INDEX_1_OFFSET - OMITTED_BMP_INDEX_1_LENGTH) + (codePoint >> SHIFT_1)];
    index = data[index + ((codePoint >> SHIFT_2) & INDEX_2_MASK)];
    index = (index << INDEX_SHIFT) + (codePoint & DATA_MASK);
    return data[index];
  }
  return data[data.length - DATA_GRANULARITY];
}

function parseGraphemeBreakProperties(source) {
  const values = new Uint8Array(0x110000);
  forEachPropertyRange(source, (start, end, property) => {
    const value = GCB[property];
    if (value === undefined) {
      fail(`Unknown Grapheme_Cluster_Break property ${property}.`);
    }
    values.fill(value, start, end + 1);
  });
  return values;
}

function parseBinaryProperty(source, selectedProperty) {
  const values = new Uint8Array(0x110000);
  forEachPropertyRange(source, (start, end, property) => {
    if (property === selectedProperty) {
      values.fill(1, start, end + 1);
    }
  });
  return values;
}

function forEachPropertyRange(source, callback) {
  for (const rawLine of source.split(/\r?\n/u)) {
    const line = rawLine.slice(0, rawLine.indexOf('#') >= 0 ? rawLine.indexOf('#') : rawLine.length).trim();
    if (line.length === 0) {
      continue;
    }
    const match = /^([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*([A-Za-z_]+)/u.exec(line);
    if (!match) {
      fail(`Could not parse Unicode property line: ${rawLine}`);
    }
    callback(Number.parseInt(match[1], 16), Number.parseInt(match[2] ?? match[1], 16), match[3]);
  }
}

function createPackedInfo(trie, graphemeBreaks, extendedPictographic) {
  const result = new Uint8Array(0x110000);
  for (let codePoint = 0; codePoint <= 0x10FFFF; codePoint++) {
    const widthInfo = (getTrieInfo(trie, codePoint) & 0x30) >> 4;
    result[codePoint] = widthInfo |
      (graphemeBreaks[codePoint] << GCB_SHIFT) |
      (extendedPictographic[codePoint] ? EXTENDED_PICTOGRAPHIC : 0);
  }
  return result;
}

function createRuns(values) {
  const starts = [0];
  const runValues = [values[0]];
  let previous = values[0];
  for (let codePoint = 1; codePoint < values.length; codePoint++) {
    if (values[codePoint] === previous) {
      continue;
    }
    starts.push(codePoint);
    runValues.push(values[codePoint]);
    previous = values[codePoint];
  }
  return { starts, values: runValues };
}

function hashValidScalars(values) {
  const hash = crypto.createHash('sha256');
  for (let codePoint = 0; codePoint <= 0x10FFFF; codePoint++) {
    if (codePoint >= 0xD800 && codePoint <= 0xDFFF) {
      continue;
    }
    hash.update(values.subarray(codePoint, codePoint + 1));
  }
  return hash.digest('hex').toUpperCase();
}

function validateGraphemeBreakTests(source, packedInfo) {
  let count = 0;
  for (const rawLine of source.split(/\r?\n/u)) {
    const body = rawLine.slice(0, rawLine.indexOf('#') >= 0 ? rawLine.indexOf('#') : rawLine.length).trim();
    if (body.length === 0) {
      continue;
    }
    const tokens = body.split(/\s+/u);
    let preceding = 0;
    for (let index = 1; index < tokens.length; index += 2) {
      const codePoint = Number.parseInt(tokens[index], 16);
      const properties = getProperties(codePoint, preceding, packedInfo, true, false);
      const expectedJoin = tokens[index - 1] === '×';
      if (properties.join !== expectedJoin) {
        fail(
          `Unicode grapheme test ${count + 1} failed before ${formatHex(codePoint)}: ` +
          `expected ${expectedJoin ? 'join' : 'break'} in ${body}.`
        );
      }
      preceding = encodeProperties(properties.state, properties.width, properties.join);
    }
    if (tokens.at(-1) !== '÷') {
      fail(`Unicode grapheme test ${count + 1} does not end with a break marker.`);
    }
    count++;
  }
  return count;
}

function getProperties(codePoint, preceding, packedInfo, handleGraphemes, ambiguousWide) {
  const info = packedInfo[codePoint];
  const gcb = (info & GCB_MASK) >> GCB_SHIFT;
  const extPict = (info & EXTENDED_PICTOGRAPHIC) !== 0;
  let width = resolvePrintWidth(codePoint, info, ambiguousWide);
  if (!handleGraphemes) {
    return { state: 0, width, join: false };
  }

  const previousState = decodeState(preceding);
  const previousGcb = previousState & STATE_GCB_MASK;
  const join = preceding !== 0 && shouldJoin(previousState, previousGcb, gcb, extPict);
  if (join) {
    width = Math.max(width, decodeWidth(preceding));
    if (previousGcb === GCB.Regional_Indicator && gcb === GCB.Regional_Indicator) {
      width = 2;
    }
  }
  return { state: nextState(previousState, previousGcb, gcb, extPict), width, join };
}

function shouldJoin(previousState, previousGcb, currentGcb, currentExtPict) {
  if (previousGcb === GCB.CR && currentGcb === GCB.LF) {
    return true;
  }
  if (isControl(previousGcb) || isControl(currentGcb)) {
    return false;
  }
  if (previousGcb === GCB.L &&
      [GCB.L, GCB.V, GCB.LV, GCB.LVT].includes(currentGcb)) {
    return true;
  }
  if ([GCB.LV, GCB.V].includes(previousGcb) && [GCB.V, GCB.T].includes(currentGcb)) {
    return true;
  }
  if ([GCB.LVT, GCB.T].includes(previousGcb) && currentGcb === GCB.T) {
    return true;
  }
  if (currentGcb === GCB.Extend || currentGcb === GCB.ZWJ || currentGcb === GCB.SpacingMark) {
    return true;
  }
  if (previousGcb === GCB.Prepend) {
    return true;
  }
  const emojiState = (previousState & STATE_EMOJI_MASK) >> STATE_EMOJI_SHIFT;
  if (emojiState === EMOJI_ZWJ_SEQUENCE && currentExtPict) {
    return true;
  }
  return previousGcb === GCB.Regional_Indicator &&
    currentGcb === GCB.Regional_Indicator &&
    (previousState & STATE_RI_ODD) !== 0;
}

function nextState(previousState, previousGcb, currentGcb, currentExtPict) {
  let state = currentGcb;
  if (currentGcb === GCB.Regional_Indicator) {
    const previousOdd = (previousState & STATE_RI_ODD) !== 0;
    if (previousGcb !== GCB.Regional_Indicator || !previousOdd) {
      state |= STATE_RI_ODD;
    }
  }

  const previousEmoji = (previousState & STATE_EMOJI_MASK) >> STATE_EMOJI_SHIFT;
  let emoji = EMOJI_NONE;
  if (currentExtPict) {
    emoji = EMOJI_EXTEND_SEQUENCE;
  } else if (currentGcb === GCB.Extend && previousEmoji === EMOJI_EXTEND_SEQUENCE) {
    emoji = EMOJI_EXTEND_SEQUENCE;
  } else if (currentGcb === GCB.ZWJ && previousEmoji === EMOJI_EXTEND_SEQUENCE) {
    emoji = EMOJI_ZWJ_SEQUENCE;
  }
  return state | (emoji << STATE_EMOJI_SHIFT);
}

function isControl(gcb) {
  return gcb === GCB.CR || gcb === GCB.LF || gcb === GCB.Control;
}

function resolvePrintWidth(codePoint, info, ambiguousWide) {
  const widthInfo = info & WIDTH_MASK;
  if (widthInfo >= 2) {
    return widthInfo === 3 || ambiguousWide || codePoint === 0xFE0F ? 2 : 1;
  }
  return 1;
}

function encodeProperties(state, width, join) {
  return ((state & 0xFFFFFF) << 3) | ((width & 3) << 1) | (join ? 1 : 0);
}

function decodeState(properties) {
  return properties >> 3;
}

function decodeWidth(properties) {
  return (properties >> 1) & 3;
}

function renderCSharp(
  runs,
  scalarPropertySha256,
  graphemeTestCount,
  upstreamHash,
  graphemeHash,
  graphemeTestHash,
  emojiHash
) {
  return `// <auto-generated />
// Generated by tools/generate-unicode-v15.mjs from pinned xterm.js width data and Unicode 15.0.0.
// xterm.js commit: ${EXPECTED_COMMIT}
// xterm.js UnicodeProperties.ts SHA-256: ${upstreamHash}
// GraphemeBreakProperty.txt SHA-256: ${graphemeHash}
// GraphemeBreakTest.txt SHA-256: ${graphemeTestHash}
// emoji-data.txt SHA-256: ${emojiHash}

namespace XtermSharp.Unicode;

internal static class UnicodeV15Data
{
    internal const int ScalarCount = ${0x110000 - 0x800};
    internal const int GraphemeBreakTestCount = ${graphemeTestCount};
    internal const string ScalarPropertySha256 = "${scalarPropertySha256}";

${renderIntField('RunStarts', runs.starts)}

${renderByteField('RunValues', runs.values)}
}
`;
}

function renderIntField(name, values) {
  const lines = [];
  for (let index = 0; index < values.length; index += 8) {
    lines.push(`        ${values.slice(index, index + 8).map(formatHex).join(', ')},`);
  }
  return `    internal static readonly int[] ${name} =
    [
${lines.join('\n')}
    ];`;
}

function renderByteField(name, values) {
  const lines = [];
  for (let index = 0; index < values.length; index += 16) {
    lines.push(`        ${values.slice(index, index + 16).map(value => `0x${value.toString(16).toUpperCase().padStart(2, '0')}`).join(', ')},`);
  }
  return `    internal static readonly byte[] ${name} =
    [
${lines.join('\n')}
    ];`;
}

function sourceHash(source) {
  return crypto.createHash('sha256').update(source).digest('hex').toUpperCase();
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
