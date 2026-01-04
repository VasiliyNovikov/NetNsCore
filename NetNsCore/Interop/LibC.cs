using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using LinuxCore;

[assembly: DisableRuntimeMarshalling]

namespace NetNsCore.Interop;

internal static unsafe partial class LibC
{
    public const int CLONE_NEWNET = 0x40000000;

    public const ulong MS_BIND = 4096;

    public const int MNT_DETACH = 2; // Just detach from the tree

    // int unshare (int __flags)
    [LibraryImport(LinuxLibraries.LibC, EntryPoint = "unshare")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressGCTransition]
    public static partial LinuxResult unshare(int flags);

    // int setns (int __fd, int __nstype)
    [LibraryImport(LinuxLibraries.LibC, EntryPoint = "setns")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressGCTransition]
    public static partial LinuxResult setns(FileDescriptor fd, int nstype);

    // int mount (const char *__special_file, const char *__dir, const char *__fstype, unsigned long int __rwflag, const void *__data)
    [LibraryImport(LinuxLibraries.LibC, EntryPoint = "mount", StringMarshalling = StringMarshalling.Utf8)]
    public static partial LinuxResult mount(string specialFile, string dir, string? fstype, ulong rwflag, void* data);

    // int umount2 (const char *__special_file, int __flags)
    [LibraryImport(LinuxLibraries.LibC, EntryPoint = "umount2", StringMarshalling = StringMarshalling.Utf8)]
    public static partial LinuxResult umount2(string specialFile, int flags);
}