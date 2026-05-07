"""
Verify that a minimal PIO program using SET PINS can drive a GPIO pin.

The program sets PIN 0 high once, then stalls (blocking PULL with empty FIFO).
We read back via machine.Pin to confirm the output was driven.
"""
import rp2
from machine import Pin

@rp2.asm_pio(set_init=rp2.PIO.OUT_LOW)
def set_pin_high():
    set(pins, 1)   # drive pin high
    wrap_target()
    nop()          # idle loop
    wrap()

sm = rp2.StateMachine(0, set_pin_high, set_base=Pin(16))
sm.active(1)

# Give the SM a few cycles to execute
import time
time.sleep_ms(1)

p = Pin(16, Pin.IN)
print("pin:", p.value())   # expected: 1
