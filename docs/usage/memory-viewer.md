# Memory Viewer

`Debug > Memory Viewer…` (Ctrl+M) opens a hex / ASCII view of the full
64K address space as a companion to the [debugger](debugger.md).

- 16 bytes per row, address column on the left, two 8-byte hex groups
  in the middle, ASCII column on the right.
- Live updates: values change as the program runs so you can watch
  RAM mutate in real time.
- The row containing **PC** is shaded pale yellow with an orange marker
  under the current byte; the row containing **SP** is shaded pale blue
  with a blue marker. Quick **PC** and **SP** buttons jump there.
- *Goto $XXXX* scrolls anywhere in the 64K space.
- `$E000-$E00F` (the PPI/PIT I/O window) is shown as `--` rather than
  read through, because real reads of those bytes have hardware side
  effects (latching PIT counters, scanning the keyboard).
