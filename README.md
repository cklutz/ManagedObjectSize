# ManagedObjectSize

**Note:** _This library depends on internals of the CoreCLR. Currently tested with version 6.0, there is no guarantee it will work with future versions. You would be ill adviced, would you use this functionality for anything else than diagnostics or other, non vital to the core of your application, features._

[![Windows](https://github.com/cklutz/ManagedObjectSize/actions/workflows/windows.yml/badge.svg)](https://github.com/cklutz/ManagedObjectSize/actions/workflows/windows.yml)
[![Ubuntu](https://github.com/cklutz/ManagedObjectSize/actions/workflows/ubuntu.yml/badge.svg)](https://github.com/cklutz/ManagedObjectSize/actions/workflows/ubuntu.yml)

Attempts to calculate the size of managed options (heap size) from within an application.

The algorithm and ideas are based largely on work from

- [ClrMD](https://github.com/microsoft/clrmd)
- The dotnet [runtime](https://github.com/dotnet/runtime)

Basically, compare this library to the SOS `!ObjSize` WinDBG extension, but callable
directly from inside the application.
