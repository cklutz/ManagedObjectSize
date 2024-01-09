# ManagedObjectSize

[![Windows](https://github.com/cklutz/ManagedObjectSize/actions/workflows/build-windows.yml/badge.svg)](https://github.com/cklutz/ManagedObjectSize/actions/workflows/build-windows.yml)
[![Ubuntu](https://github.com/cklutz/ManagedObjectSize/actions/workflows/build-ubuntu.yml/badge.svg)](https://github.com/cklutz/ManagedObjectSize/actions/workflows/build-ubuntu.yml)

_This library depends on internals of the CoreCLR. Currently tested with version 6.0, 7.0 and 8.0, there is no guarantee it will work with future versions. You would be ill adviced, would you use this functionality for anything else than diagnostics or other, non vital to the core of your application, features._

Attempts to calculate the size of managed options (heap size) from within an application. Basically, compare this library to the SOS `!ObjSize` WinDBG extension, but callable
directly from inside the application.

There are two packages. The `ManagedObjectSize` package contains the core functionality. And provides stubs to use your own object pooling implementation with it.
The `ManagedObjectSize.ObjectPool` package provides ready-to-use helpers to use `Microsoft.Extensions.ObjectPool` as the object pooling implementation. This way
the core package has no further dependencies itself.

|Package                       | Latest  | Stable |
|------------------------------|---------|--------|
| ManagedObjectSize            | [![Nuget](https://img.shields.io/nuget/vpre/ManagedObjectSize)](https://www.nuget.org/packages/ManagedObjectSize/) | [![Nuget](https://img.shields.io/nuget/v/ManagedObjectSize)](https://www.nuget.org/packages/ManagedObjectSize/) |
| ManagedObjectSize.ObjectPool | [![Nuget](https://img.shields.io/nuget/vpre/ManagedObjectSize.ObjectPool)](https://www.nuget.org/packages/ManagedObjectSize.ObjectPool/) | [![Nuget](https://img.shields.io/nuget/v/ManagedObjectSize.ObjectPool)](https://www.nuget.org/packages/ManagedObjectSize.ObjectPool/) |


The algorithm and ideas are based largely on work from

- [ClrMD](https://github.com/microsoft/clrmd)
- The dotnet [runtime](https://github.com/dotnet/runtime)




