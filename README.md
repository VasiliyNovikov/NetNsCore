# NetNsCore

A lightweight C# library for managing Linux network namespaces. Wraps `unshare`, `setns`, `mount`, and `umount2` syscalls with a clean, disposable API. AOT-compatible, Linux-only, .NET 10+.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![NetNsCore release](https://img.shields.io/nuget/v/NetNsCore)](https://www.nuget.org/packages/NetNsCore/)
[![NetNsCore download count](https://img.shields.io/nuget/dt/NetNsCore)](https://www.nuget.org/packages/NetNsCore/)

## Usage

```csharp
using NetNsCore;

// Create and enter a namespace — automatically restored on dispose
NetNs.Create("my_ns");
using (NetNs.Enter("my_ns"))
{
    // code runs inside "my_ns"
}
NetNs.Delete("my_ns");
```

## API

| Member | Description |
|---|---|
| `Create(name)` | Create a named network namespace |
| `Delete(name)` | Delete a named network namespace |
| `Exists(name)` | Check whether a named namespace exists |
| `List()` | List all namespace paths under `/run/netns/` |
| `Open(name)` | Open a handle to a named namespace |
| `OpenCurrent()` | Open a handle to the current thread's namespace |
| `OpenRoot()` | Open a handle to the root (PID 1) namespace |
| `Enter(name)` | Enter a namespace; returns a `Scope` that restores the original on dispose |
| `EnterRoot()` | Enter the root namespace |
| `ns.Enter()` | Enter via handle |
| `ns.Clone()` | Clone handle (duplicates the file descriptor) |
| `ns.CreateSocket(af, type, proto)` | Create a `Socket` inside this namespace |
| `ns.CreateSocket(type, proto)` | Create a dual-mode `Socket` inside this namespace |
| `CreateSocket(name, af, type, proto)` | Create a `Socket` inside a named namespace |
| `CreateSocket(name, type, proto)` | Create a dual-mode `Socket` inside a named namespace |
| `ns.Id` | Unique `UInt128` identifier |
| `ns.Descriptor` | Underlying `FileDescriptor` |
