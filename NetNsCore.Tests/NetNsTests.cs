using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetNsCore.Tests;

[TestClass]
public class NetNsTests
{
    [TestMethod]
    public void NetNs_Add()
    {
        const string testNsName = "test_ns";
        using var _ = new TestNsScope(testNsName);
        NetNs.Create(testNsName);
        Assert.IsTrue(IsNetNsExists(testNsName));
    }

    [TestMethod]
    public void NetNs_Delete()
    {
        const string testNsName = "test_ns_2";
        using var _ = new TestNsScope(testNsName);
        Script.Exec("ip", "netns", "add", testNsName);
        NetNs.Delete(testNsName);
        Assert.IsFalse(IsNetNsExists(testNsName));
    }

    [TestMethod]
    public void NetNs_Exists()
    {
        const string testNsName = "test_ns_3";
        Assert.IsFalse(NetNs.Exists(testNsName));
        using var _ = new TestNsScope(testNsName);
        Script.Exec("ip", "netns", "add", testNsName);
        Assert.IsTrue(NetNs.Exists(testNsName));
        Script.Exec("ip", "netns", "delete", testNsName);
        Assert.IsFalse(NetNs.Exists(testNsName));
    }

    [TestMethod]
    public void NetNs_Enter()
    {
        const string testNsName = "test_ns_4";
        using var _ = new TestNsScope(testNsName);
        Assert.IsNull(GetCurrentNetNs());
        NetNs.Create(testNsName);
        Assert.IsNull(GetCurrentNetNs());
        using (NetNs.Enter(testNsName))
            Assert.AreEqual(testNsName, GetCurrentNetNs());
        Assert.IsNull(GetCurrentNetNs());
    }

    [TestMethod]
    public void NetNs_EnterRoot()
    {
        const string testNsName = "test_ns_5";
        using var _ = new TestNsScope(testNsName);
        Script.Exec("ip", "netns", "add", testNsName);
        using (NetNs.Enter(testNsName))
        {
            Assert.AreEqual(testNsName, GetCurrentNetNs());
            using (NetNs.EnterRoot())
                Assert.IsNull(GetCurrentNetNs());
            Assert.AreEqual(testNsName, GetCurrentNetNs());
        }
        Assert.IsNull(GetCurrentNetNs());
    }

    [TestMethod]
    public void NetNs_Clone()
    {
        const string testNsName = "test_ns_6";
        using var _ = new TestNsScope(testNsName);
        Script.Exec("ip", "netns", "add", testNsName);
        using var ns = NetNs.Open(testNsName);
        using var cloneNs = ns.Clone();
        Assert.AreEqual(ns.Id, cloneNs.Id);
        Assert.AreNotEqual(ns.Descriptor, cloneNs.Descriptor);
    }

    [TestMethod]
    public void NetNs_CreateSocket()
    {
        const string testNsName = "test_ns_7";
        using var _ = new TestNsScope(testNsName);
        NetNs.Create(testNsName);
        Script.Exec("ip", "netns", "exec", testNsName, "ip", "link", "set", "lo", "up");
        using var listenSocket = NetNs.CreateSocket(testNsName, AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listenSocket.Listen();
        var endpoint = (IPEndPoint)listenSocket.LocalEndPoint!;

        // Connection from root namespace should fail
        Assert.IsNull(GetCurrentNetNs());
        using var rootSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Assert.ThrowsExactly<SocketException>(() => rootSocket.Connect(endpoint));

        // Connection from test namespace should succeed
        using (NetNs.Enter(testNsName))
        {
            using var nsSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            nsSocket.Connect(endpoint);
            Assert.IsTrue(nsSocket.Connected);
        }
    }

    private static bool IsNetNsExists(string nsName)
    {
        return Script.ExecLines("ip", "netns", "list").Any(n => n.StartsWith(nsName, StringComparison.Ordinal));
    }

    private static string? GetCurrentNetNs()
    {
        var currentNs = Script.Exec("ip", "netns", "identify");
        return currentNs == "" ? null : currentNs;
    }

    private sealed class TestNsScope(string testNsName) : IDisposable
    {
        public void Dispose() => Script.ExecNoThrow("ip", "netns", "delete", testNsName);
    }
}
