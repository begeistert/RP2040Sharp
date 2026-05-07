"""
Verify PIO FIFO loopback: OUT from OSR → GPIO pins, then IN from GPIO pins → ISR → RX FIFO.

Uses a two-SM loopback pattern entirely in software (no physical wire needed):
  SM0: OUT PINS 8 — shifts 8 bits from OSR to 8 GPIO pins
  SM1: IN  PINS 8 — shifts 8 bits from the same GPIO pins into ISR, then PUSH

Both state machines share the same 8-pin window (pin_base=0, count=8).
We write a value to SM0 TX FIFO and read it back from SM1 RX FIFO.
"""
import rp2
from machine import Pin

@rp2.asm_pio(out_init=[rp2.PIO.OUT_LOW]*8, autopull=True, pull_thresh=8)
def out8():
    out(pins, 8)
    wrap_target()
    nop()
    wrap()

@rp2.asm_pio(in_shiftdir=rp2.PIO.SHIFT_LEFT, autopush=True, push_thresh=8)
def in8():
    wrap_target()
    in_(pins, 8)
    wrap()

sm0 = rp2.StateMachine(0, out8,  freq=10_000_000, out_base=Pin(0))
sm1 = rp2.StateMachine(1, in8,   freq=10_000_000, in_base=Pin(0))

sm0.active(1)
sm1.active(1)

SENTINEL = 0xA5
sm0.put(SENTINEL)

import time
time.sleep_ms(5)

result = sm1.get()
print("loopback:", hex(result & 0xFF))   # expected: 0xa5
