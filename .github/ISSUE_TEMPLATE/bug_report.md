---
name: 🐛 Bug Report
about: Create a report to help us improve RP2040Sharp
title: '[BUG] '
labels: bug
assignees: ''
---

**Describe the bug** <br/>
A clear and concise description of what the bug is.

**To Reproduce** <br/>
Steps to reproduce the behavior:
1. Load instruction/binary: `0x....`
2. Run CPU for `X` cycles
3. Check Register `R...`
4. See error

**Expected behavior** <br/>
A clear and concise description of what you expected to happen (e.g., "R0 should be 0x10, but got 0x00").

**Code Snippet / Opcode**
```csharp
// Paste the failing test case or the instruction hex here
var opcode = 0x1234;
cpu.Step();
```

**Environment** <br/>
* OS: [e.g. Windows 11, Ubuntu 22.04]
* .NET Version: [e.g. 10.0 Preview]
* Branch: [e.g. master]

**Aditional context** <br/>
Add any other context about the problem here. Reference specific sections of the RP2040 datasheet if relevant.
