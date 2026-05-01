from machine import SPI, Pin
import time

spi = SPI(0, baudrate=1000000, polarity=0, phase=0, sck=Pin(2), mosi=Pin(3), miso=Pin(4))
cs  = Pin(5, Pin.OUT)

messages = [b'\x01\x02\x03', b'Hello, SPI!', b'\xDE\xAD\xBE\xEF']

for msg in messages:
    cs.value(0)
    spi.write(msg)
    cs.value(1)
    time.sleep_ms(10)

print("SPI done")
