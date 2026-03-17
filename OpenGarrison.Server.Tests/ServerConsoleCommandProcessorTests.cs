using System;
using System.Collections.Generic;
using System.Threading;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class ServerConsoleCommandProcessorTests
{
    [Fact]
    public void TryProcessLine_DoesNotShutdownWhenRedirectedInputCloses()
    {
        using var shutdownCts = new CancellationTokenSource();
        var messages = new List<string>();

        var shouldContinue = ServerConsoleCommandProcessor.TryProcessLine(
            null,
            isInputRedirected: true,
            shutdownCts,
            messages.Add);

        Assert.False(shouldContinue);
        Assert.False(shutdownCts.IsCancellationRequested);
        Assert.Contains("[server] stdin closed; command listener stopped.", messages);
    }

    [Fact]
    public void TryProcessLine_ShutsDownOnShutdownCommand()
    {
        using var shutdownCts = new CancellationTokenSource();
        var messages = new List<string>();

        var shouldContinue = ServerConsoleCommandProcessor.TryProcessLine(
            "shutdown",
            isInputRedirected: true,
            shutdownCts,
            messages.Add);

        Assert.False(shouldContinue);
        Assert.True(shutdownCts.IsCancellationRequested);
        Assert.Contains("[server] shutdown requested.", messages);
    }

    [Fact]
    public void TryProcessLine_AllowsBomPrefixedShutdownCommand()
    {
        using var shutdownCts = new CancellationTokenSource();
        var messages = new List<string>();

        var shouldContinue = ServerConsoleCommandProcessor.TryProcessLine(
            "\uFEFFshutdown",
            isInputRedirected: true,
            shutdownCts,
            messages.Add);

        Assert.False(shouldContinue);
        Assert.True(shutdownCts.IsCancellationRequested);
        Assert.DoesNotContain(messages, message => message.Contains("unknown command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryProcessLine_ForwardsNonShutdownCommandToQueue()
    {
        using var shutdownCts = new CancellationTokenSource();
        var messages = new List<string>();
        string? queuedCommand = null;

        var shouldContinue = ServerConsoleCommandProcessor.TryProcessLine(
            "status",
            isInputRedirected: true,
            shutdownCts,
            messages.Add,
            command => queuedCommand = command);

        Assert.True(shouldContinue);
        Assert.False(shutdownCts.IsCancellationRequested);
        Assert.Equal("status", queuedCommand);
        Assert.Empty(messages);
    }
}
