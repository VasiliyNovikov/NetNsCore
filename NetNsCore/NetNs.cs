using System;
using System.IO;
using System.Numerics;

using LinuxCore;

using NetNsCore.Interop;

namespace NetNsCore;

public sealed class NetNs : IDisposable, IEquatable<NetNs>, IEqualityOperators<NetNs, NetNs, bool>
{
    private const LinuxFileMode NetNsBasePathMode = (LinuxFileMode)0x1ED; // 0755
    private const LinuxFileMode NetNsFileMode = (LinuxFileMode)0x124; // 0444
    private const string NetNsBasePath = "/run/netns";
    private const string SelfThreadNsNetPath = "/proc/thread-self/ns/net";
    private const string SelfThreadFdPath = "/proc/thread-self/fd";
    private const string RootNsNetPath = "/proc/1/ns/net";

    private readonly LinuxFile _nsFile;

    public UInt128 Id => new(_nsFile.DeviceId, _nsFile.INode);

    public FileDescriptor Descriptor => _nsFile.Descriptor;

    private NetNs(string path) => _nsFile = new LinuxFile(path, LinuxFileFlags.ReadOnly);
    private NetNs(FileDescriptor descriptor) => _nsFile = new LinuxFile(descriptor);

    public void Dispose() => _nsFile.Dispose();

    public NetNs Clone() => new(Descriptor.Clone());

    public Scope Enter() => new(this);

    public override int GetHashCode() => _nsFile.INode.GetHashCode();

    public bool Equals(NetNs? other) => other is not null && _nsFile.DeviceId == other._nsFile.DeviceId && _nsFile.INode == other._nsFile.INode;

    public override bool Equals(object? obj) => obj is NetNs other && Equals(other);

    public static bool operator ==(NetNs? left, NetNs? right) => left is null && right is null || left is not null && left.Equals(right);

    public static bool operator !=(NetNs? left, NetNs? right) => !(left == right);

    public static unsafe void Create(string name)
    {
        // Ensure the base path exists
        Directory.CreateDirectory(NetNsBasePath, (UnixFileMode)NetNsBasePathMode);

        // Keep a handle to the original netns so we can switch back later
        using var oldNs = OpenCurrent();

        var target = Path.Combine(NetNsBasePath, name);
        try
        {
            // Create the target file (regular file is fine) that we'll bind-mount onto
            using (new LinuxFile(target, LinuxFileFlags.Create | LinuxFileFlags.Exclusive | LinuxFileFlags.ReadWrite, NetNsFileMode)) { }

            // Create a new network namespace for *this thread*
            LibC.unshare(LibC.CLONE_NEWNET).ThrowIfError();
            try
            {
                // Open a handle to the *new* netns we just created
                using var ns = OpenCurrent();

                // Bind-mount the new netns to /run/netns/<name> to persist it.
                // Using the /proc/thread-self/fd/<nsFd> path ensures we bind the FD we opened.
                var srcPath = Path.Combine(SelfThreadFdPath, ns.Descriptor.ToString());
                LibC.mount(srcPath, target, null, LibC.MS_BIND, null).ThrowIfError();
            }
            finally
            {
                oldNs.Set();
            }
        }
        catch
        {
            if (File.Exists(target))
                File.Delete(target);
            throw;
        }
    }

    public static void Delete(string name)
    {
        var target = Path.Combine(NetNsBasePath, name);
        // Unmount the netns file
        LibC.umount2(target, LibC.MNT_DETACH).ThrowIfError();
        File.Delete(target);
    }

    public static bool Exists(string name) => File.Exists(Path.Combine(NetNsBasePath, name));

    public static bool ReCreate(string name)
    {
        var existed = Exists(name);
        if (existed)
            Delete(name);
        Create(name);
        return existed;
    }

    public static string[] List() => Directory.Exists(NetNsBasePath) ? Directory.GetFiles(NetNsBasePath) : [];

    public static Scope Enter(string name)
    {
        using var ns = Open(name);
        return ns.Enter();
    }

    public static Scope EnterRoot()
    {
        using var ns = OpenRoot();
        return ns.Enter();
    }

    public static NetNs OpenCurrent() => new(SelfThreadNsNetPath);

    public static NetNs OpenRoot() => new(RootNsNetPath);

    public static NetNs Open(string name) => new(Path.Combine(NetNsBasePath, name));

    private void Set() => LibC.setns(Descriptor, LibC.CLONE_NEWNET).ThrowIfError();

    public readonly ref struct Scope : IDisposable
    {
        private readonly NetNs _old;

        internal Scope(NetNs ns)
        {
            _old = OpenCurrent();
            ns.Set();
        }

        public void Dispose()
        {
            try
            {
                _old.Set();
            }
            finally
            {
                _old.Dispose();
            }
        }
    }
}