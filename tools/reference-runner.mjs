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
const terminal = new Terminal({ allowProposedApi: true, ...(request.options ?? {}) });
const events = [];
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
    default:
      throw new Error(`Unknown operation '${operation.type}'`);
  }
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
          foreground: cell?.getFgColor() ?? 0,
          backgroundMode: cell ? color(cell, false) : 'default',
          background: cell?.getBgColor() ?? 0,
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

process.stdout.write(JSON.stringify({
  columns: terminal.cols,
  rows: terminal.rows,
  activeBuffer: terminal.buffer.active.type,
  modes: terminal.modes,
  normal: snapshotBuffer(terminal.buffer.normal),
  alternate: snapshotBuffer(terminal.buffer.alternate),
  events
}));

terminal.dispose();

