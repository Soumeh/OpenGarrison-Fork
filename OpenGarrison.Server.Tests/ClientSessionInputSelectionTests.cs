using System;
using System.Net;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class ClientSessionInputSelectionTests
{
    [Fact]
    public void TryGetInputForNextTick_UsesNewestReceivedInput()
    {
        var session = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero);
        var olderInput = new PlayerInputSnapshot(Left: true, Right: false, Up: false, Down: false, BuildSentry: false, DestroySentry: false, Taunt: false, FirePrimary: false, FireSecondary: false, AimWorldX: 10f, AimWorldY: 20f, DebugKill: false);
        var newerInput = new PlayerInputSnapshot(Left: false, Right: true, Up: false, Down: false, BuildSentry: false, DestroySentry: false, Taunt: false, FirePrimary: true, FireSecondary: false, AimWorldX: 30f, AimWorldY: 40f, DebugKill: false);

        Assert.True(session.TrySetLatestInput(10, olderInput));
        Assert.True(session.TrySetLatestInput(11, newerInput));

        Assert.True(session.TryGetInputForNextTick(out var appliedInput));
        Assert.Equal(newerInput, appliedInput);
        Assert.Equal<uint>(11, session.LastProcessedInputSequence);
    }

    [Fact]
    public void TryGetInputForNextTick_ReusesLatestAppliedInputWhenNoNewPacketArrived()
    {
        var session = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero);
        var input = new PlayerInputSnapshot(Left: false, Right: true, Up: false, Down: false, BuildSentry: false, DestroySentry: false, Taunt: false, FirePrimary: false, FireSecondary: false, AimWorldX: 50f, AimWorldY: 60f, DebugKill: false);

        Assert.True(session.TrySetLatestInput(25, input));
        Assert.True(session.TryGetInputForNextTick(out var firstTickInput));
        Assert.Equal(input, firstTickInput);
        Assert.Equal<uint>(25, session.LastProcessedInputSequence);

        Assert.True(session.TryGetInputForNextTick(out var secondTickInput));
        Assert.Equal(input, secondTickInput);
        Assert.Equal<uint>(25, session.LastProcessedInputSequence);
    }

    [Fact]
    public void TrySetLatestInput_IgnoresOutOfOrderSequences()
    {
        var session = new ClientSession(1, new IPEndPoint(IPAddress.Loopback, 8190), "Player", TimeSpan.Zero);
        var newestInput = new PlayerInputSnapshot(Left: false, Right: true, Up: false, Down: false, BuildSentry: false, DestroySentry: false, Taunt: false, FirePrimary: false, FireSecondary: false, AimWorldX: 10f, AimWorldY: 10f, DebugKill: false);
        var staleInput = newestInput with { Left = true, Right = false, AimWorldX = 20f };

        Assert.True(session.TrySetLatestInput(50, newestInput));
        Assert.False(session.TrySetLatestInput(49, staleInput));

        Assert.True(session.TryGetInputForNextTick(out var consumedInput));
        Assert.Equal(newestInput, consumedInput);
        Assert.Equal<uint>(50, session.LastProcessedInputSequence);
    }
}
