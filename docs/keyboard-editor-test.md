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

Open via **Debug → Key Capture Test…**. The capture box at the top of
the dialog highlights light-yellow when focused; the details panel
below shows the parsed `KeyData`, bare VK, modifier mask, resolved
char (if any), and the L/R-ambiguity flag.

**Printable keys (KeyDown → KeyPress path):**

- [ ] Press `a` — captured as `A` with char `'a'` (U+0061). No
      ambiguity warning.
- [ ] Press `Shift+a` — captured as `A, Shift` with char `'A'`
      (U+0041).
- [ ] Press `1`, then `Shift+1` — capture switches between `'1'` and
      `'!'` correctly.

**Non-printable keys (KeyDown-only path):**

- [ ] **Esc** — captured (would normally close a dialog; doesn't here).
      Char field reads "(none)".
- [ ] **Enter** — captured; doesn't act as the dialog's default-button.
- [ ] **Tab** — captured; does NOT navigate to the next control.
- [ ] **Arrow keys** — all four captured individually; do not move
      focus.
- [ ] **F1–F12** — each captured.
- [ ] **Insert / Delete / Home / End / PgUp / PgDn** — each captured.

**Modifier handling:**

- [ ] Press and hold **Ctrl** alone — capture box reads
      `Ctrl held — release to bind alone, or press another key`; no
      `Captured` event yet.
- [ ] **Release Ctrl** without pressing anything else (tap-to-bind) —
      captured as `Ctrl`; ambiguity note appears in orange.
- [ ] With Ctrl still held, press `g` — captured as `Ctrl+G` with a
      char value (PC Ctrl+G resolves to U+0007).
- [ ] Same tap-to-bind / modifier+letter check for **Shift** and **Alt**.
- [ ] **Alt+letter** captures without a Windows error ding — confirms
      the host form's `ProcessCmdKey` Alt-forward is wired.

**Ambiguity warning (decision #8):**

- [ ] **Tap Ctrl alone** → orange note `Left/Right Ctrl share this
      binding — both will fire.`
- [ ] **Tap Shift alone** → orange note for Shift.
- [ ] **Tap Alt alone** → orange note for Alt.
- [ ] **Ctrl+G** → orange note `Ctrl modifier bound generically — left
      and right both trigger.`
- [ ] **Shift+Ctrl+G** → orange note covers `Ctrl / Shift` together.
- [ ] Non-modifier keys alone (letters, digits, function keys,
      cursors) do NOT show the orange ambiguity note.

**Friendly names:**

- [ ] **Page Up / Page Down** show as `Page Up` / `Page Down` (not the
      WinForms `Prior` / `Next` names).
- [ ] Tapping **Left Ctrl** specifically: capture line reads `Ctrl`
      (normalised to generic); the orange ambiguity note explains why.

**Reset button:**

- [ ] After any capture, click **Reset** — capture box returns to the
      `Click here, then press a key…` prompt; details panel reads
      `Capture cleared.`; focus is returned to the capture box.

**Close behaviour:**

- [ ] **Close** button closes the dialog; emulator main window still
      responds to keystrokes afterwards.

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
