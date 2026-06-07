# Keyboard

By default, the emulator drives the MZ-700 matrix from the **character**
your PC keystroke produces (after Windows has applied your keyboard
layout). Type `;` and the MZ sees `;`; type `+` and it sees `+`. Out of
the box there is nothing to configure — the host OS handles layout,
AltGr, and dead keys for you, and the emulator translates the resulting
Unicode character to the corresponding MZ-700 matrix position and shift
state.

Printable coverage includes the alphanumeric keys, common punctuation,
and the row-1 brackets/braces `[` `]` `{` `}`. `*` lives on the
MZ-700's shift-colon key, so typing `*` on the PC keyboard produces `*`
on the MZ.

Non-character keys are mapped directly:

| PC key | MZ-700 |
|---|---|
| Enter | CR |
| Backspace / Delete | DEL |
| Insert | INS |
| Esc | BREAK (use **Shift+Esc** to break a running BASIC program — the manual requires BREAK to be shifted) |
| Cursor keys | Cursor |
| Left Ctrl | MZ Ctrl |
| F1–F4 | F1–F4 |
| F11 | GRAPH (mode-toggle) |
| F12 | ALPHA (mode-toggle) |

## Editing the mapping

Open **Settings → Keyboard** (or **Ctrl+S → Keyboard tab**). The primary
view is a clickable diagram of the real MZ-700 keyboard. Each cap shows
the PC key(s) currently bound to it as a small blue badge in the top-
right corner.

**Click any key** to open the per-key editor. Character keys (1, A, `;`,
…) get a two-card view — one for the unshifted glyph and one for the
shifted glyph — each with its current PC binding and the built-in
default it would revert to. Fixed-label keys (CR, GRAPH, ALPHA, CTRL,
SHIFT, BREAK, INST, DEL, cursors) get a single card.

Each slot card has two actions:
- **Edit…** opens a capture dialog. Click the capture box, press the PC
  key (or modifier combo, e.g. Ctrl+G) you want to bind. The dialog
  confirms what it captured and notes any conflict — if your captured
  key already produces a different MZ slot, you get a warning and a
  confirmation prompt before Save replaces the old binding.
- **Reset** removes any override you've added for the slot and reverts
  it to the built-in default shown beneath the current binding.

Edits are live the moment you Save — the diagram redraws to reflect the
new binding, and the emulator picks it up on the next keystroke.
Persistence to `settings.ini` waits for the dialog's **Apply** or
**OK**, matching the rest of Settings.

## Safety gate

If you rebind PC keys so that some essential MZ key ends up with **no**
PC binding at all (e.g. moving PC `1` to a different slot without
restoring a binding for MZ `1` first), the affected MZ key is outlined
in crimson on the diagram. A character key needs both its unshifted and
shifted glyphs covered to count as reachable.

When you click **Apply** or **OK**, the dialog lists the unreachable
keys and asks whether to save anyway. Pick **No** to keep editing.

## Font Sheet — typing GRAPH-mode glyphs

The MZ-700 character ROM contains graphic and kana glyphs that have no
PC-key equivalent. When the machine enters GRAPH mode (press the GRAPH
key — defaults to **F11**), the **Font Sheet** window pops up
automatically. It shows all 512 glyphs (two banks of 256) at 3× scale.
Click a glyph to type its display code into the emulator via the
auto-typer.

The Font Sheet is also always available from **View → Font Sheet…**
(**Ctrl+G**) so you can browse glyphs in ALPHA mode too.

## Import / Export

The Export… and Import… buttons on the Keyboard tab read and write
small `.mzkbd` files — useful for sharing a mapping or switching
between layouts.

The file is a tiny INI; only the user's overrides are stored (defaults
are still applied at runtime). Two sections, the same per-line shapes
used inside `settings.ini`:

```ini
; MZ-700 Keyboard mapping file
[CharMap]
; HHHH=Row,Col,Shift   (HHHH = 4-digit hex Unicode codepoint;
;                       Shift = t | f)
0031=0,1,f   ; '1'
0021=0,1,t   ; '!'

[KeyOverrides]
; KeyName=Row,Col,Shift   (KeyName from System.Windows.Forms.Keys;
;                          Shift = t | f | -)
F5=8,7,t
Control, G=0,6,-
```

Import offers **Merge** (your imported entries win where they overlap)
or **Replace** (clear current overrides first). Changes go live
immediately; Apply / OK still gates persistence.

## Advanced — matrix grid

The Settings → Keyboard tab has an **Advanced — matrix grid** expander
underneath the diagram. It exposes the raw 10×8 MZ-700 keyboard matrix
with live highlight of which bits are currently asserted. Click a
matrix cell to edit the character binding for that specific
`(row, col, shift)` slot. It's the same character editor the diagram
uses; the matrix view is just an alternative entry point for slot-by-
slot work.

## Where bindings live

| Layer | File location | What it stores |
|---|---|---|
| Built-in char defaults | `Hardware/CharMap.cs` | PC character → MZ slot for printables |
| Built-in VK defaults | `Hardware/SpecialKeyMap.cs` | PC virtual key → MZ slot for non-printables |
| User char overrides | `[CharMap]` in `settings.ini` | Your changes to the char defaults |
| User VK overrides | `[KeyOverrides]` in `settings.ini` | Your changes to the VK defaults |
| Portable export | `*.mzkbd` (anywhere) | Snapshot of just your overrides |

The user override layers are consulted ahead of the built-in defaults,
so any edit you make in the dialog wins at runtime.

## Loading BASIC source

"Load BASIC source…" (Ctrl+Shift+B) reads a plain-text `.bas` file and
types each non-blank, non-comment line into the running BASIC
interpreter. Lines starting with `;` or `'` are stripped on the host
side. If BASIC isn't loaded yet the emulator resets, auto-loads BASIC,
then types the source once the READY prompt is up. End the file with
`RUN` to auto-start the program.

Per-character throughput is around 6–8 chars/sec — the auto-typer
waits for the OS's keyboard scan to actually observe each press before
moving on, rather than hardcoding a hold duration. Still not instant
for long listings, but reliable across shifted characters.
