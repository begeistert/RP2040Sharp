"""
Verify PIO TX→RX FIFO round-trip using a MOV OSR ISR program.

The program:
  PULL         — move TX FIFO word into OSR
  MOV ISR, OSR — copy OSR to ISR
  PUSH         — move ISR into RX FIFO

This exercises both halves of the FIFO path with a single SM.
"""
import rp2

@rp2.asm_pio()
def copy_tx_to_rx():
    wrap_target()
    pull(block)
    mov(isr, osr)
    push(block)
    wrap()

sm = rp2.StateMachine(0, copy_tx_to_rx)
sm.active(1)

SENTINEL = 0xDEAD_BEEF
sm.put(SENTINEL)

import time
time.sleep_ms(2)

result = sm.get()
print("fifo:", hex(result))   # expected: 0xdeadbeef
