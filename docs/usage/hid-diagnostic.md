# HID Diagnostic

`Debug > HID Diagnostic…` (Ctrl+H) opens a live view of host input and
how it lands on the MZ-700 keyboard matrix. Useful for "why isn't this
key working", verifying joystick button mappings, or watching the
GRAPH/ALPHA mode bit flip in real time. The window doesn't grab focus
on open, so you can keep typing into the emulator and watch the
diagnostic update.

Three panes, all refreshed each frame:

- **Host input (Windows side).** The last `KeyData` seen (e.g.
  `Control, ShiftKey, G`) with its numeric value, the last KeyPress
  character, the last KeyUp, and the modifier keys currently held.
  Below that, joystick state per slot: configured SW1/SW2 button
  indices, raw WinMM axes (X, Y as 0–255) and the raw button bitmask,
  plus the resolved SW1/SW2 bits.
- **Mapping (which layer matched).** Which of the three keyboard
  layers handled the last keypress — `Override` (user-editable
  physical-key map), `SpecialKey` (built-in non-character keys like
  cursors, F-keys, GRAPH/ALPHA), `Character` (the char-driven path
  through `CharMap`), or `None` if nothing matched. The resolved
  `(row, col, MzShift)` is shown alongside, with shift recorded as
  ON / OFF / pass-through.
- **MZ-700 side.** The full 10×8 keyboard matrix as bit rows (`0` =
  pressed, since the matrix is active-low), with each row's hex byte
  alongside for at-a-glance reading. Below the grid: the last row the
  ROM scanned, the `$1170` shift-mirror byte the monitor uses to pick
  between the shifted and unshifted lookup tables, and the `$0060`
  mode flag decoded to `ALPHA` / `GRAPH` (only meaningful once S-BASIC
  is loaded — before that, `$0060` is ROM and the decode is just
  noise).

The window doesn't replay history — it just shows the *most recent*
event of each kind, so press one key at a time when investigating a
specific behaviour.
