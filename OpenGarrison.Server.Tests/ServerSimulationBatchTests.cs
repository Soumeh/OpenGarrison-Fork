using System;
using System.Collections.Generic;
using System.Net;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class ServerSimulationBatchTests
{
    [Fact]
    public void Advance_WhenCatchUpOccurs_BroadcastsOnlyLatestSnapshot()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var clientsBySlot = new Dictionary<byte, ClientSession>
        {
            [SimulationWorld.LocalPlayerSlot] = new(
                SimulationWorld.LocalPlayerSlot,
                new IPEndPoint(IPAddress.Loopback, 8190),
                "Player",
                TimeSpan.Zero),
        };
        var sentSnapshots = new List<SnapshotMessage>();
        var broadcaster = new SnapshotBroadcaster(
            world,
            config,
            clientsBySlot,
            transientEventReplayTicks: 4,
            (_, snapshot, _) =>
            {
                sentSnapshots.Add(snapshot);
            });
        var simulator = new FixedStepSimulator(world);

        var ticks = ServerSimulationBatch.Advance(
            simulator,
            config.FixedDeltaSeconds * 2.1d,
            beforeTickAdvanced: static () => { },
            onTickAdvanced: static () => { },
            broadcaster.BroadcastSnapshot);

        Assert.Equal(2, ticks);
        var snapshot = Assert.Single(sentSnapshots);
        Assert.Equal<ulong>(2, snapshot.Frame);
    }

    [Fact]
    public void Advance_WhenNoTicksAreReady_DoesNotBroadcastSnapshot()
    {
        var world = new SimulationWorld();
        var config = world.Config;
        var sentSnapshots = 0;
        var simulator = new FixedStepSimulator(world);

        var ticks = ServerSimulationBatch.Advance(
            simulator,
            config.FixedDeltaSeconds * 0.25d,
            beforeTickAdvanced: static () => { },
            onTickAdvanced: static () => { },
            () => sentSnapshots += 1);

        Assert.Equal(0, ticks);
        Assert.Equal(0, sentSnapshots);
    }
}
