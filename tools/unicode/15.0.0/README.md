# Unicode 15.0 acceptance data

These files pin the Unicode 15.0 data used to generate and verify XtermSharp's
`15-graphemes` provider:

- `GraphemeBreakProperty.txt` from
  `https://www.unicode.org/Public/15.0.0/ucd/auxiliary/GraphemeBreakProperty.txt`
- `GraphemeBreakTest.txt` from
  `https://www.unicode.org/Public/15.0.0/ucd/auxiliary/GraphemeBreakTest.txt`
- `emoji-data.txt` from
  `https://www.unicode.org/Public/15.0.0/ucd/emoji/emoji-data.txt`

`tools/generate-unicode-v15.mjs` verifies the file headers, records their SHA-256 hashes in the
generated C# data, combines them with the pinned xterm.js width trie and validates all official
grapheme-break test rows before writing `UnicodeV15Data.cs`.

The Unicode data files are distributed under the terms in `LICENSE.txt`.
