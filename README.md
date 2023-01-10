# ManagedObjectSize

Attempts to calculate the size of managed options (heap size) from within an application.

The algorithm and ideas are based largely on work from

- [ClrMD](https://github.com/microsoft/clrmd)
- The dotnet [runtime](https://github.com/dotnet/runtime)

Basically, compare this library to the SOS `!ObjSize` WinDBG extension, but callable
directly from inside the application.
