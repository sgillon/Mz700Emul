# Hardware notes

A handful of MZ-700 hardware quirks the code learned the hard way and
documents inline. Captured here for posterity and as a starting point
for anyone trying to understand why the emulator does things a
particular way.

- The PIT topology is C0 standalone (audio at ~895 kHz); C1 standalone
  (rate generator at 15.6 kHz, OUT1 → CLK2); C2 cascaded from C1.OUT
  (12-hour RTC, OUT2 → CPU INT). Earlier "obvious" wirings were wrong.
- `$E008` bit 0 is the 555/556 cursor-osc / "tempo" signal at ~50 Hz
  toggle (tuned against real-hardware MUSIC playback length), **not**
  the 8253 OUT1.
- BASIC's MUSIC duration polls `$E008` bit 0 — getting that signal's
  rate right is what makes a 13-second tune actually take 13 seconds.
- BASIC's text-area pointer (TXTTAB) lives at `$6ABF`; the cassette
  loader updates it so programs whose load address differs from the
  default `$6BCF` (e.g. `trek.mzf` at `$6BDF`) `LIST` and `RUN`
  correctly.
- `$E008` bits 1-6 are joystick lines (active-low, via an LS367 buffer)
  and must default to **1** ("idle / not pressed") when no joystick is
  connected. Returning 0 there makes joystick games (e.g.
  `panic.mzf`) auto-start and run with all directions held.
