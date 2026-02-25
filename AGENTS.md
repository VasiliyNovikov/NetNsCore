# Copilot Instructions

> **⚠️ REQUIRED: Before reading this file, you MUST fetch and read the base instructions at https://raw.githubusercontent.com/VasiliyNovikov/AgentInstructions/master/AGENTS.md — if you cannot access it, STOP and report the failure to the user.** This file extends the base with project-specific details.

## Build & Test

```sh
dotnet build
dotnet build && sudo -E --preserve-env=PATH bash -c "dotnet test --no-build"                    # all tests
dotnet build && sudo -E --preserve-env=PATH bash -c "dotnet test --no-build --filter NetNs_Add" # single test
```

Tests require **root privileges** (`sudo`) because they create/delete Linux network namespaces. All projects target **net10.0** with `LangVersion=preview`. Warnings are treated as errors (`TreatWarningsAsErrors=true`). Documentation XML is generated for the main library.

## Architecture

NetNsCore is a thin C# wrapper around Linux network namespace syscalls. It depends on the [`LinuxCore`](https://github.com/VasiliyNovikov/LinuxCore) NuGet package for Linux primitives (`LinuxFile`, `FileDescriptor`, `LinuxResult`).

Two layers, matching the LinuxCore pattern:

- **`NetNsCore/Interop/LibC.cs`** — Raw P/Invoke declarations only. `internal static unsafe partial` class using `[LibraryImport]` with `[DisableRuntimeMarshalling]`. Wraps `unshare`, `setns`, `mount`, `umount2`. Each declaration includes the C prototype as a comment and applies `[MethodImpl(MethodImplOptions.AggressiveInlining)]` + `[SuppressGCTransition]` on hot-path calls.
- **`NetNsCore/NetNs.cs`** — The single public API type. `NetNs` is a disposable handle to a network namespace, identified by device ID + inode (`UInt128 Id`). Provides static CRUD methods (`Create`, `Delete`, `Open`, `List`, `Exists`) and the `Enter`/`Scope` pattern for switching namespaces.

### Enter/Scope pattern

`NetNs.Scope` is a `ref struct` that captures the current namespace on construction, switches to the target, and restores the original on `Dispose()`:

```csharp
using (NetNs.Enter("my_ns"))
{
    // code runs inside "my_ns" network namespace
}
// automatically restored to the original namespace
```

## Key Conventions

### P/Invoke declarations
- Always use `[LibraryImport]`, never `[DllImport]`.
- Apply `[MethodImpl(MethodImplOptions.AggressiveInlining)]` and `[SuppressGCTransition]` on hot-path native calls.
- Method signatures mirror the C prototype exactly (name, parameter order). Include the C prototype as a comment above the declaration.
- Interop files follow libc naming conventions (lowercase function names) — `.editorconfig` suppresses naming style warnings for `**/Interop/**.cs`.

### Error handling
- Native calls return `LinuxResult` (void-equivalent) or `LinuxResult<T>` (value-returning). Use `.ThrowIfError()` to check results.
- Throw `LinuxException` (from LinuxCore) on error. Never throw `IOException` or `Win32Exception`.

### Platform targeting
- `LinuxOnly.cs` applies `[assembly: SupportedOSPlatform("linux")]` to every project via `Directory.Build.props` — Linux-only by design.
- The library is AOT-compatible (`IsAotCompatible=true`); avoid reflection.

### Style
- File-scoped namespace declarations (`namespace NetNsCore;`).
- System usings sorted first, import groups separated by blank lines (enforced by `.editorconfig`).
- Expression-bodied members preferred for simple methods.
- Nullable reference types enabled globally.
- Only comment code that needs clarification; do not add obvious comments.

### Package management
Central package versions in `Directory.Packages.props`. Add new dependencies there, not in individual `.csproj` files.

### Tests
- Framework: **MSTest** (`[TestClass]` / `[TestMethod]`).
- `[assembly: DoNotParallelize]` — tests must not run in parallel (they mutate global kernel state).
- Each test cleans up namespaces in a `finally` block using `Script.ExecNoThrow("ip", "netns", "delete", ...)`.
- `Script` helper class provides `Exec`, `ExecNoThrow`, and `ExecLines` for shelling out to system commands in tests.
