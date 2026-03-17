using System;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class PlayerEntitySpyCloakTests
{
    [Fact]
    public void SpyCloak_MustFadePastHalfBeforeDecloaking()
    {
        var player = CreateSpy();

        Assert.True(player.TryToggleSpyCloak());

        AdvanceTicks(player, 9);
        Assert.InRange(player.SpyCloakAlpha, 0.54f, 0.56f);
        Assert.False(player.TryToggleSpyCloak());

        AdvanceTicks(player, 1);
        Assert.InRange(player.SpyCloakAlpha, 0.49f, 0.51f);
        Assert.True(player.TryToggleSpyCloak());
    }

    [Fact]
    public void SpyCloak_DamageRevealBumpsFullyCloakedSpyBackIntoView()
    {
        var player = CreateSpy();

        Assert.True(player.TryToggleSpyCloak());
        AdvanceTicks(player, 20);
        Assert.InRange(player.SpyCloakAlpha, 0f, 0.001f);

        var died = player.ApplyDamage(5, PlayerEntity.SpyDamageRevealAlpha);

        Assert.False(died);
        Assert.InRange(player.SpyCloakAlpha, 0.09f, 0.11f);
        Assert.True(player.IsSpyVisibleToEnemies);
    }

    private static PlayerEntity CreateSpy()
    {
        var player = new PlayerEntity(7, CharacterClassCatalog.Spy, "Spy");
        player.Spawn(PlayerTeam.Red, 100f, 100f);
        return player;
    }

    private static void AdvanceTicks(PlayerEntity player, int ticks)
    {
        var level = CreateLevel();
        var deltaSeconds = 1d / SimulationConfig.DefaultTicksPerSecond;
        for (var tick = 0; tick < ticks; tick += 1)
        {
            player.Advance(default, jumpPressed: false, level, player.Team, deltaSeconds);
        }
    }

    private static SimpleLevel CreateLevel()
    {
        var spawn = new SpawnPoint(100f, 100f);
        return new SimpleLevel(
            name: "SpyCloakTest",
            mode: GameModeKind.CaptureTheFlag,
            bounds: new WorldBounds(1000f, 600f),
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [spawn],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: Array.Empty<RoomObjectMarker>(),
            floorY: 500f,
            solids: Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }
}
