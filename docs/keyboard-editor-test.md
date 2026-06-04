# Keyboard editor — development test sheet

Manual checks accumulated step by step as Phase A of the keyboard-map
editor work lands. Run the relevant section after each commit; add new
checks as new behaviour ships. Once Phase A is complete the keepers
get distilled into `docs/release-check.md`.

Conventions:
- "READY prompt" means launched with `--basic` and the BASIC `READY`
  banner is showing.
- Codes in this sheet are MZ-700 display codes (not ASCII). Position
  in the Font Sheet implies the code: `row * 16 + col` within the bank.

---

## A1 — Font Sheet diagnostic

- [ ] **Debug → Font Sheet…** opens without stealing focus from a
      typing-in-progress emulator.
- [ ] Bank 0 (top half) and Bank 1 (bottom half) both render; each is
      a 16×16 grid with column headers `0..F` and row headers `0_..F_`.
- [ ] Glyphs in bank 0 are visually identical to what the same display
      code produces on the live MZ screen during normal use.
- [ ] **Reload** button re-renders without artefacts (cache flush works).

## A2 — MzGlyphCatalog

Data-only; no direct manual check. Verified indirectly when the matrix
grid lands at A6.

## A2.5 / A2.6 — ROM key tables + click-to-input

Boot to a READY prompt before each click test.

- [ ] **Click `A`** (bank 0 row 4 col 1): status reports
      `Typed bank 0 code $41 via (4,7).`; `A` appears at the cursor.
- [ ] **Click `*`** (bank 0 row 6 col B): status reports shift-typed;
      `*` appears at the cursor.
- [ ] **Click `#`** (bank 0 row 2 col 3): status reports shift-typed;
      `#` appears at the cursor.
- [ ] **Click a digit** (e.g. `5` at bank 0 row 3 col 5): appears at
      the cursor.
- [ ] **Click a graphics glyph** (e.g. racing car at bank 0 row C col 9):
      status reads `… isn't reachable from the keyboard.`; nothing
      appears in BASIC. *(Proper graphics-glyph typing needs the
      GRAPH-mode ROM tables wired up — out of scope for Phase A.)*
- [ ] Same behaviour with the main window in **GRAPH mode** as in
      **ALPHA mode** (mode keys themselves should not be re-bindable
      from the Font Sheet — they're filtered as special-key slots).
- [ ] Status text correctly reflects shift state in the parenthesised
      slot (`shift+(row,col)` for shifted entries).

## A3 — CharMapOverrides

Data-only plumbing; no direct manual check. Verified at A4 via the INI
round-trip (write an override, restart, confirm it survives and is
honoured by `CharMap.TryLookup`).

## A4 — INI persistence and section retrofit

**File shape (run once, then inspect `settings.ini` next to the exe):**

- [ ] All five sections present in order: `[Display]`, `[Roms]`,
      `[Joystick]`, `[KeyOverrides]`, `[CharMap]`.
- [ ] Every section has a comment block immediately under its header
      describing its values, allowed ranges, and what each token means.
      A fresh reader should understand the file without opening source
      code.
- [ ] `[KeyOverrides]` shift documents `t / f / -` (pass-through is
      meaningful here); `[CharMap]` shift documents only `t / f`
      (pass-through is explicitly noted as not applicable).

**Override round-trip (manual edit, then restart):**

- [ ] Add a single line to `[CharMap]`: `0061=6,0,f   ; 'a'` (rebinds
      PC `a` to slot (6,0), which is `.`).
- [ ] Restart the emulator and bring up the BASIC `READY` prompt.
- [ ] Press `a` on the PC keyboard. A `.` (period) should appear at
      the cursor, not `A`.
- [ ] Remove the line from `[CharMap]` and restart. Pressing `a` now
      produces `A` again (default behaviour restored).

**Inline-comment safety:**

- [ ] The `; 'a'` glyph comment on the override line does not break
      parsing — the entry loads correctly.

**Upgrade-from-older-INI check:**

- [ ] Take an older `settings.ini` (e.g. from before A4) that lacks
      the `[CharMap]` section. Launch. The file is rewritten with the
      new comment blocks and `[CharMap]` present; existing values
      (display scale, joystick buttons, key overrides) are preserved.

## A5 — KeyCaptureControl

*(to be added)*

## A6 — KeyboardMatrixGrid

*(to be added)*

## A7 — Settings → Keyboard tab

*(to be added)*

## A8 — KeyBindingEditorForm

*(to be added)*

## A9 — Reset buttons

*(to be added)*

## A10 — Apply safety gate

*(to be added)*

## A11 — Import / Export `.mzkbd`

*(to be added)*

## A12 — Docs

*(to be added)*
