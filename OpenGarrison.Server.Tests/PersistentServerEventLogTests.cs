using System;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class PersistentServerEventLogTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "OpenGarrison-server-event-log-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetDefaultPath_UsesLogsDirectoryAndDateStamp()
    {
        var path = PersistentServerEventLog.GetDefaultPath(new DateTimeOffset(2026, 3, 15, 11, 45, 0, TimeSpan.Zero));

        Assert.Equal(RuntimePaths.LogsDirectory, Path.GetDirectoryName(path));
        Assert.Equal("server-events-20260315.log", Path.GetFileName(path));
    }

    [Fact]
    public void Write_AppendsStructuredEscapedLines()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "events.log");

        using (var log = new PersistentServerEventLog(path))
        {
            log.Write(
                "chat_received",
                ("player_name", "Alice \"Spy\"\nTest"),
                ("slot", (byte)2),
                ("authorized", true));
            log.Write(
                "score_changed",
                ("red_caps", 3),
                ("blue_caps", 2),
                ("mode", GameModeKind.CaptureTheFlag));
        }

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("timestamp=\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("event=\"chat_received\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("player_name=\"Alice \\\"Spy\\\"\\nTest\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("slot=2", lines[0], StringComparison.Ordinal);
        Assert.Contains("authorized=true", lines[0], StringComparison.Ordinal);
        Assert.Contains("event=\"score_changed\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("red_caps=3", lines[1], StringComparison.Ordinal);
        Assert.Contains("blue_caps=2", lines[1], StringComparison.Ordinal);
        Assert.Contains("mode=\"CaptureTheFlag\"", lines[1], StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
