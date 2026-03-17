using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class PlayerEntitySpyBackstabTests
{
    [Fact]
    public void TryConsumeSpyBackstabHitboxTrigger_PreservesFullRecoveryWindow()
    {
        var player = CreateSpy();

        AdvanceSpyTicks(player, PlayerEntity.SpyBackstabWindupTicksDefault);

        Assert.Equal(0, player.SpyBackstabWindupTicksRemaining);
        Assert.Equal(PlayerEntity.SpyBackstabRecoveryTicksDefault, player.SpyBackstabRecoveryTicksRemaining);
        Assert.True(player.TryConsumeSpyBackstabHitboxTrigger(out _));
        Assert.Equal(PlayerEntity.SpyBackstabRecoveryTicksDefault, player.SpyBackstabRecoveryTicksRemaining);
        Assert.False(player.TryConsumeSpyBackstabHitboxTrigger(out _));

        AdvanceSpyTicks(player, 1);

        Assert.Equal(PlayerEntity.SpyBackstabRecoveryTicksDefault - 1, player.SpyBackstabRecoveryTicksRemaining);
    }

    [Fact]
    public void SpyBackstab_StaysLockedUntilVisualFinishes()
    {
        var player = CreateSpy();

        AdvanceSpyTicks(player, PlayerEntity.SpyBackstabWindupTicksDefault + PlayerEntity.SpyBackstabRecoveryTicksDefault);

        Assert.True(player.IsSpyBackstabReady);
        Assert.True(player.IsSpyBackstabAnimating);
        Assert.False(player.TryToggleSpyCloak());

        var lockedX = player.X;
        AdvanceOneTick(player, input: default(PlayerInputSnapshot) with { Right = true });

        Assert.Equal(lockedX, player.X);

        AdvanceSpyTicks(player, PlayerEntity.SpyBackstabVisualTicksDefault - PlayerEntity.SpyBackstabWindupTicksDefault - PlayerEntity.SpyBackstabRecoveryTicksDefault);

        Assert.False(player.IsSpyBackstabAnimating);
        Assert.Equal(0, player.SpyBackstabVisualTicksRemaining);
        Assert.True(player.TryToggleSpyCloak());
    }

    [Fact]
    public void StabAnimationEntity_UsesSourceStyleLifetime()
    {
        var animation = new StabAnimEntity(1, 2, PlayerTeam.Red, 100f, 200f, 0f);

        AdvanceAnimation(animation, StabAnimEntity.WarmupTicks);
        Assert.Equal(0, animation.FrameIndex);
        Assert.True(animation.Alpha > 0.01f);

        AdvanceAnimation(animation, 1);
        Assert.Equal(1, animation.FrameIndex);

        AdvanceAnimation(animation, StabAnimEntity.SwingTicks - 1);
        Assert.Equal(StabAnimEntity.SwingTicks, animation.FrameIndex);
        Assert.True(animation.Alpha > 0f);

        AdvanceAnimation(animation, StabAnimEntity.FadeOutTicks);
        Assert.True(animation.IsExpired);
        Assert.Equal(0f, animation.Alpha);
    }

    private static PlayerEntity CreateSpy()
    {
        var player = new PlayerEntity(33, CharacterClassCatalog.Spy, "Spy");
        player.Spawn(PlayerTeam.Red, 100f, 220f);
        Assert.True(player.TryToggleSpyCloak());
        Assert.True(player.TryStartSpyBackstab(0f));
        return player;
    }

    private static void AdvanceSpyTicks(PlayerEntity player, int ticks)
    {
        for (var index = 0; index < ticks; index += 1)
        {
            AdvanceOneTick(player);
        }
    }

    private static void AdvanceOneTick(PlayerEntity player, PlayerInputSnapshot input = default)
    {
        player.Advance(
            input,
            jumpPressed: false,
            SimpleLevelFactory.CreateScoutPrototypeLevel(),
            player.Team,
            1d / LegacyMovementModel.SourceTicksPerSecond);
    }

    private static void AdvanceAnimation(StabAnimEntity animation, int ticks)
    {
        for (var index = 0; index < ticks; index += 1)
        {
            animation.AdvanceOneTick(100f, 200f);
        }
    }
}
