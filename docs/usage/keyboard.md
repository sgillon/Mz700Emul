# Keyboard

The emulator drives the MZ-700 matrix from the **character** your PC
keystroke produces, after Windows has applied your keyboard layout.
Type `;` and the MZ sees `;`; type `+` and it sees `+`; type `:` and it
sees `:`. There is no per-key configuration — the host OS handles
layout, AltGr, and dead keys for you, and the emulator translates the
resulting Unicode character to the corresponding MZ-700 matrix
position (and shift state, where needed).

Non-character keys are mapped directly:

| PC key | MZ-700 |
|---|---|
| Enter | CR |
| Backspace / Delete | DEL |
| Insert | INS |
| Esc | BREAK |
| Cursor keys | Cursor |
| Left/Right Ctrl | MZ Ctrl |
| F1–F4 | F1–F4 |

The translation table lives in `Hardware/CharMap.cs` (printables) and
`Hardware/SpecialKeyMap.cs` (non-printables). MZ-only glyphs (graphics
blocks, kana) aren't reachable in this scheme — that's an intentional
trade-off for not having to configure anything.

## Loading BASIC source

"Load BASIC source…" (Ctrl+Shift+B) reads a plain-text `.bas` file and
types each non-blank, non-comment line into the running BASIC
interpreter. Lines starting with `;` or `'` are stripped on the host
side. If BASIC isn't loaded yet the emulator resets, auto-loads BASIC,
then types the source once the READY prompt is up. End the file with
`RUN` to auto-start the program.

This feature is currently very slow, but will hopefully improve in the future.
