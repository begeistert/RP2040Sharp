import struct, textwrap

bootrom = bytearray(16384)

def u32(buf, offset, val):
    struct.pack_into('<I', buf, offset, val)

def u16(buf, offset, val):
    struct.pack_into('<H', buf, offset, val)

DEFAULT_HANDLER = 0x0181  # BX LR at 0x0180 | Thumb bit
BOOTROM_SP      = 0x20041000

# Exception vector table (0x0000 - 0x003F)
u32(bootrom, 0x0000, BOOTROM_SP)
for i in range(1, 16):
    u32(bootrom, 0x0004 + (i-1)*4, DEFAULT_HANDLER)
for i in range(26):
    u32(bootrom, 0x0040 + i*4, DEFAULT_HANDLER)

# ROM API infrastructure slots (within reserved vector area)
u16(bootrom, 0x0010, 0x0210)  # ROM code magic
u16(bootrom, 0x0012, 0x02)    # ROM version = 2
u16(bootrom, 0x0014, 0x0200)  # func_table_ptr
u16(bootrom, 0x0016, 0x0250)  # data_table_ptr
u16(bootrom, 0x0018, 0x0060)  # rom_table_lookup fn ptr

# default_handler / no-op function at 0x0180: BX LR
u16(bootrom, 0x0180, 0x4770)  # BX LR

# rom_table_lookup at 0x0060
# r0 = table (uint16_t*), r1 = code
# Loop over {code, ptr} pairs; return ptr or 0
LOOKUP = [
    0x8802,  # LDRH r2, [r0, #0]       ; loop:
    0xB13A,  # CBZ r2, not_found        ; if zero -> end
    0xB28B,  # UXTH r3, r1             ; r3 = code & 0xFFFF
    0x429A,  # CMP r2, r3
    0xD002,  # BEQ found
    0x3004,  # ADDS r0, r0, #4
    0xE7F9,  # B loop
    0x8840,  # LDRH r0, [r0, #2]       ; found:
    0x4770,  # BX LR
    0x2000,  # MOVS r0, #0             ; not_found:
    0x4770,  # BX LR
]
for i, op in enumerate(LOOKUP):
    u16(bootrom, 0x0060 + i*2, op)

# memcpy44 at 0x0100
# void *memcpy44(void *dst, void *src, uint n)  -- n bytes, multiple of 4
MEMCPY = [
    0xB510,  # PUSH {r4, lr}
    0x4604,  # MOV r4, r0        ; save dst
    0xC908,  # LDMIA r1!, {r3}   ; loop: r3 = *src++
    0xC008,  # STMIA r0!, {r3}   ; *dst++ = r3
    0x3A04,  # SUBS r2, r2, #4
    0xD1FC,  # BNE loop          ; offset -8 to LDMIA (index 2)
    0x4620,  # MOV r0, r4
    0xBD10,  # POP {r4, pc}
]
# BNE at index 5 (offset 10): PC_next=12, loop=4, delta=(4-12)/2=-4, 0xFC -> D1FC
for i, op in enumerate(MEMCPY):
    u16(bootrom, 0x0100 + i*2, op)

# memset4 at 0x0120
# void *memset4(void *dst, uint8_t c, uint n) -- n bytes, multiple of 4
MEMSET = [
    0xB510,  # PUSH {r4, lr}
    0x4604,  # MOV r4, r0          ; save dst
    0xB249,  # UXTB r1, r1         ; r1 = c & 0xFF
    0x020B,  # LSLS r3, r1, #8     ; r3 = c << 8
    0x4319,  # ORRS r1, r3         ; r1 = c|(c<<8)
    0x040B,  # LSLS r3, r1, #16
    0x4319,  # ORRS r1, r3         ; r1 = 4-byte word
    0xE001,  # B test               ; -> SUBS first
    0xC002,  # STMIA r0!, {r1}      ; loop: *dst++ = word
    0x3A04,  # SUBS r2, r2, #4      ; test:
    0xD1FD,  # BNE loop             ; offset -6 to STMIA (index 8)
    0x4620,  # MOV r0, r4
    0xBD10,  # POP {r4, pc}
]
# BNE at index 10 (offset 20): PC_next=22, STMIA=16, delta=(16-22)/2=-3, 0xFD -> D1FD
for i, op in enumerate(MEMSET):
    u16(bootrom, 0x0120 + i*2, op)

# Function lookup table at 0x0200: {code, ptr} pairs, terminated by {0,0}
entries = [
    (0x434D, 0x0100),  # 'MC' -> memcpy44
    (0x534D, 0x0120),  # 'MS' -> memset4
    (0x4649, 0x0180),  # 'IF' -> connect_internal_flash (noop)
    (0x5845, 0x0180),  # 'EX' -> flash_exit_xip (noop)
    (0x4346, 0x0180),  # 'FC' -> flash_flush_cache (noop)
    (0x5843, 0x0180),  # 'CX' -> flash_enter_cmd_xip (noop)
    (0x5052, 0x0180),  # 'RP' -> flash_range_program (noop)
    (0x4546, 0x0180),  # 'RE' -> flash_range_erase (noop)
    (0x0000, 0x0000),  # terminator
]
for i, (code, ptr) in enumerate(entries):
    u16(bootrom, 0x0200 + i*4, code)
    u16(bootrom, 0x0202 + i*4, ptr)

# Data table at 0x0250: just terminator
u16(bootrom, 0x0250, 0x0000)

# Emit as C# array
print("new byte[]")
print("{")
for row in range(0, len(bootrom), 16):
    chunk = bootrom[row:row+16]
    hex_str = ', '.join(f'0x{b:02X}' for b in chunk)
    print(f"    {hex_str},")
print("}")
