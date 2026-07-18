#!/usr/bin/env node
// Development-only xterm.js oracle. Build the pinned reference first with:
//   cd xterm.js && npm ci && npm run build && npm run esbuild-package-headless-only

import process from 'node:process';
import { Terminal } from '../xterm.js/headless/lib-headless/xterm-headless.mjs';

let input = '';
for await (const chunk of process.stdin) {
  input += chunk;
}

const request = JSON.parse(input);
const { unicodeVersion = '6', ...terminalOptions } = request.options ?? {};
const terminal = new Terminal({ allowProposedApi: true, ...terminalOptions });
if (unicodeVersion === '15' || unicodeVersion === '15-graphemes') {
  // The pinned addon's Node path creates a pooled Buffer, while its trie reader assumes byteOffset
  // zero. Force the browser-equivalent Uint8Array path so the development oracle is deterministic.
  const savedBuffer = globalThis.Buffer;
  let UnicodeGraphemesAddon;
  try {
    globalThis.Buffer = undefined;
    ({ UnicodeGraphemesAddon } = await import(
      '../xterm.js/addons/addon-unicode-graphemes/lib/addon-unicode-graphemes.mjs'
    ));
  } finally {
    globalThis.Buffer = savedBuffer;
  }
  terminal.loadAddon(new UnicodeGraphemesAddon());
  terminal.unicode.activeVersion = unicodeVersion;
}
const events = [];
const markers = [];
const observedLinkIds = new Set();
terminal.onBell(() => events.push({ type: 'bell' }));
terminal.onData(data => events.push({ type: 'data', data }));
terminal.onCursorMove(() => events.push({ type: 'cursor' }));
terminal.onLineFeed(() => events.push({ type: 'lineFeed' }));
terminal.onResize(({ cols, rows }) => events.push({ type: 'resize', cols, rows }));
terminal.onScroll(viewportY => events.push({ type: 'scroll', viewportY }));
terminal.onTitleChange(title => events.push({ type: 'title', title }));

for (const operation of request.operations ?? []) {
  switch (operation.type) {
    case 'write':
      await new Promise(resolve => terminal.write(operation.data ?? '', resolve));
      break;
    case 'writeBytes':
      await new Promise(resolve => terminal.write(Uint8Array.from(operation.data ?? []), resolve));
      break;
    case 'resize':
      terminal.resize(operation.columns, operation.rows);
      break;
    case 'reset':
      terminal.reset();
      break;
    case 'clear':
      terminal.clear();
      break;
    case 'scrollLines':
      terminal.scrollLines(operation.amount);
      break;
    case 'scrollToLine':
      terminal.scrollToLine(operation.line);
      break;
    case 'registerMarker':
      markers.push({
        name: operation.name ?? '',
        marker: terminal.registerMarker(operation.cursorYOffset ?? 0)
      });
      break;
    default:
      throw new Error(`Unknown operation '${operation.type}'`);
  }
  captureLinkIds(terminal.buffer.normal, observedLinkIds);
  captureLinkIds(terminal.buffer.alternate, observedLinkIds);
}

function color(cell, foreground) {
  if (foreground ? cell.isFgRGB() : cell.isBgRGB()) return 'rgb';
  if (foreground ? cell.isFgPalette() : cell.isBgPalette()) return 'palette';
  return 'default';
}

function underlineColorMode(cell) {
  const mode = (cell?.extended?.underlineColor ?? 0) & 0x3000000;
  if (mode === 0x3000000) return 'rgb';
  if (mode === 0x1000000 || mode === 0x2000000) return 'palette';
  return 'default';
}

function underlineColor(cell) {
  const value = cell?.extended?.underlineColor ?? 0;
  const mode = value & 0x3000000;
  if (mode === 0x3000000) return value & 0xFFFFFF;
  if (mode === 0x1000000 || mode === 0x2000000) return value & 0xFF;
  return -1;
}

function normalizedCodePoint(cell) {
  const chars = cell?.getChars() ?? '';
  if (!chars) return 0;
  return Array.from(chars).at(-1).codePointAt(0);
}

function captureLinkIds(buffer, linkIds) {
  const reusable = buffer.getNullCell();
  for (let y = 0; y < buffer.length; y++) {
    const line = buffer.getLine(y);
    if (!line) continue;
    for (let x = 0; x < line.length; x++) {
      const linkId = line.getCell(x, reusable)?.extended?.urlId ?? 0;
      if (linkId !== 0) linkIds.add(linkId);
    }
  }
}

function snapshotBuffer(buffer, internalBuffer) {
  const lines = [];
  const reusable = buffer.getNullCell();
  for (let y = 0; y < buffer.length; y++) {
    const line = buffer.getLine(y);
    const internalLine = internalBuffer.lines.get(y);
    const cells = [];
    if (line) {
      for (let x = 0; x < line.length; x++) {
        const cell = line.getCell(x, reusable);
        const hyperlinkId = cell?.extended?.urlId ?? 0;
        const rawUnderlineStyle = cell?.getUnderlineStyle?.() ?? 0;
        const hyperlinkOnlyUnderline = hyperlinkId !== 0 &&
          (rawUnderlineStyle === 0 || rawUnderlineStyle === 5);
        cells.push({
          text: cell?.getChars() ?? '',
          codePoint: normalizedCodePoint(cell),
          width: cell?.getWidth() ?? 1,
          foregroundMode: cell ? color(cell, true) : 'default',
          foreground: cell?.getFgColor() ?? 0,
          backgroundMode: cell ? color(cell, false) : 'default',
          background: cell?.getBgColor() ?? 0,
          bold: !!cell?.isBold(),
          dim: !!cell?.isDim(),
          italic: !!cell?.isItalic(),
          underline: !hyperlinkOnlyUnderline && !!cell?.isUnderline(),
          underlineStyle: hyperlinkOnlyUnderline ? 0 : rawUnderlineStyle,
          underlineColorMode: underlineColorMode(cell),
          underlineColor: underlineColor(cell),
          blink: !!cell?.isBlink(),
          inverse: !!cell?.isInverse(),
          invisible: !!cell?.isInvisible(),
          strikethrough: !!cell?.isStrikethrough(),
          overline: !!cell?.isOverline(),
          hyperlinkId,
          isProtected: !!internalLine?.isProtected(x)
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

process.stdout.write(JSON.stringify({
  columns: terminal.cols,
  rows: terminal.rows,
  activeBuffer: terminal.buffer.active.type,
  modes: terminal.modes,
  normal: snapshotBuffer(terminal.buffer.normal, terminal._core.buffers.normal),
  alternate: snapshotBuffer(terminal.buffer.alternate, terminal._core.buffers.alt),
  markers: markers.map(({ name, marker }) => ({
    name,
    line: marker.line,
    isDisposed: marker.isDisposed
  })),
  linkMetadata: [...observedLinkIds].sort((a, b) => a - b).map(id => {
    const data = terminal._core._oscLinkService.getLinkData(id);
    return {
      id,
      uri: data?.uri ?? null,
      explicitId: data?.id ?? null
    };
  }),
  events
}));

terminal.dispose();
