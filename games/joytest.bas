; Joystick test in BASIC - adapted from the MZ-1X03 manual's Program A.
; Move stick 1 to push the * around the screen. Press break (Esc) to
; stop. Lines starting with ; or ' are stripped on the host side, so
; comments here don't take up RAM in the emulator.
;
; To run: File > Load BASIC source... (Ctrl+Shift+B) -> games/joytest.bas
10 X=INT(JOY(0)/6.5):Y=INT(JOY(1)/10.5)
20 IF X>38 THEN X=38
30 IF Y>23 THEN Y=23
40 CURSOR X,Y:PRINT "+";
50 FOR A=0 TO 20:NEXT
60 CURSOR X,Y:PRINT " ";
70 GOTO 10
RUN
