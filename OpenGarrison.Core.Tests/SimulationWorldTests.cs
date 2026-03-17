using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class SimulationWorldTests
{
    [Fact]
    public void ForceKillLocalPlayer_RespawnsAfterDefaultDelay()
    {
        var world = CreateWorld();

        world.ForceKillLocalPlayer();

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);

        AdvanceTicks(world, 149);

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(1, world.LocalPlayerRespawnTicks);

        world.AdvanceOneTick();

        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(0, world.LocalPlayerRespawnTicks);
    }

    [Fact]
    public void TrySetLocalClass_KillsPlayerAndRespawnsAsRequestedClass()
    {
        var world = CreateWorld();

        var changed = world.TrySetLocalClass(PlayerClass.Engineer);

        Assert.True(changed);
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);
        Assert.Null(world.LocalDeathCam);
        var killFeedEntry = Assert.Single(world.KillFeed);
        Assert.Equal("Player 1 bid farewell, cruel world!", killFeedEntry.MessageText);

        AdvanceTicks(world, 150);

        Assert.Equal(PlayerClass.Engineer, world.LocalPlayer.ClassId);
        Assert.Equal("Engineer", world.LocalPlayer.ClassName);
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(world.LocalPlayer.MaxHealth, world.LocalPlayer.Health);
        Assert.Equal(world.LocalPlayer.MaxShells, world.LocalPlayer.CurrentShells);
    }

    [Fact]
    public void EngineerCanOnlyBuildOneSentryAndSpendsMetal()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        var builtFirst = world.TryBuildLocalSentry();
        var builtSecond = world.TryBuildLocalSentry();

        Assert.True(builtFirst);
        Assert.False(builtSecond);
        Assert.Single(world.Sentries);
        Assert.Equal(0f, world.LocalPlayer.Metal);
    }

    [Fact]
    public void DroppedIntel_ReturnsToBaseAfterReturnTimer()
    {
        var world = CreateWorld();

        var pickedUp = world.ForceGiveEnemyIntelToLocalPlayer();
        world.ForceDropLocalIntel();

        Assert.True(pickedUp);
        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
        Assert.Equal(900, world.BlueIntel.ReturnTicksRemaining);

        AdvanceTicks(world, 901);

        if (!world.BlueIntel.IsAtBase)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.BlueIntel.ReturnTicksRemaining <= 0);
        Assert.True(world.BlueIntel.X >= 0f);
        Assert.True(world.BlueIntel.Y >= 0f);
    }

    [Fact]
    public void KillingLocalCarrier_DropsIntelAndCreatesDeathFeedback()
    {
        var world = CreateWorld();

        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.ForceKillLocalPlayer();

        Assert.False(world.LocalPlayer.IsAlive);
        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.NotNull(world.LocalDeathCam);
        Assert.Single(world.KillFeed);
        Assert.Equal(150, world.LocalPlayerRespawnTicks);
    }

    [Fact]
    public void DeathFeedback_ExpiresAfterItsTickLifetime()
    {
        var world = CreateWorld();

        world.ForceKillLocalPlayer();

        Assert.NotNull(world.LocalDeathCam);
        Assert.Single(world.KillFeed);

        AdvanceTicks(world, 150);

        Assert.Null(world.LocalDeathCam);
        Assert.Empty(world.KillFeed);
    }

    [Fact]
    public void CarryingEnemyIntelToOwnBase_ScoresCaptureAndResetsIntel()
    {
        var world = CreateWorld();
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);

        Assert.True(ownBase.HasValue);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());

        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();

        Assert.False(world.LocalPlayer.IsCarryingIntel);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(1, world.LocalPlayer.Caps);
        Assert.True(world.BlueIntel.IsAtBase);
        Assert.False(world.BlueIntel.IsDropped);
    }

    [Fact]
    public void SetCapLimit_UpdatesRuleWithoutResettingRoundState()
    {
        var world = CreateWorld();
        var ownBase = world.Level.GetIntelBase(world.LocalPlayerTeam);

        Assert.True(ownBase.HasValue);
        Assert.True(world.ForceGiveEnemyIntelToLocalPlayer());
        world.TeleportLocalPlayer(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();
        Assert.Equal(1, world.RedCaps);

        world.SetCapLimit(8);

        Assert.Equal(8, world.MatchRules.CapLimit);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Assert.Equal(MatchPhase.Running, world.MatchState.Phase);
    }

    [Fact]
    public void LoadingArenaMap_EnablesArenaModeAndStartsLockedPointTimer()
    {
        var world = CreateWorld();

        Assert.True(world.TryLoadLevel("arena_montane"));
        Assert.Equal(GameModeKind.Arena, world.MatchRules.Mode);
        Assert.True(world.ArenaPointLocked);
        Assert.Equal(1800, world.ArenaUnlockTicksRemaining);

        world.AdvanceOneTick();

        Assert.True(world.ArenaPointLocked);
        Assert.Equal(1799, world.ArenaUnlockTicksRemaining);
    }

    [Fact]
    public void LoadingGeneratorMap_InitializesGenerators()
    {
        var world = CreateWorld();

        Assert.True(world.TryLoadLevel("destroy"));
        Assert.Equal(GameModeKind.Generator, world.MatchRules.Mode);
        Assert.Equal(2, world.Generators.Count);
        Assert.Equal(4000, world.GetGenerator(PlayerTeam.Red)!.Health);
        Assert.Equal(4000, world.GetGenerator(PlayerTeam.Blue)!.Health);
    }

    [Fact]
    public void DestroyingGenerator_AwardsCapAndEndsRound()
    {
        var world = CreateWorld();

        Assert.True(world.TryLoadLevel("destroy"));
        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);

        var destroyed = world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f);

        Assert.True(destroyed);
        Assert.True(world.GetGenerator(PlayerTeam.Blue)!.IsDestroyed);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(0, world.BlueCaps);
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.True(world.IsMapChangePending);
    }

    [Fact]
    public void DestroyingLocalSentry_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Engineer);

        Assert.True(world.TryBuildLocalSentry());

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        Assert.True(world.TryDestroyLocalSentry());

        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Explosion");
    }

    [Fact]
    public void RocketExplosion_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(20f, world.LocalPlayer.Y);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: -100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        var exploded = AdvanceUntilExplosion(world);

        Assert.True(exploded);
    }

    [Fact]
    public void RocketSelfBlast_EntersRecoveryStateUntilUpwardMotionEnds()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(30f, world.LocalPlayer.Y);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: -100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);
        Assert.True(AdvanceUntilExplosion(world));

        Assert.Equal(LegacyMovementState.ExplosionRecovery, world.LocalPlayer.MovementState);

        for (var tick = 0; tick < 60 && world.LocalPlayer.MovementState != LegacyMovementState.None; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(LegacyMovementState.None, world.LocalPlayer.MovementState);
    }

    [Fact]
    public void RocketSelfBlast_UsesSourceCadenceAtDifferentServerTickRates()
    {
        var thirtyTickWorld = CreateWorld();
        var sixtyTickWorld = CreateWorld(60);

        var thirtyTickVerticalSpeed = TriggerPointBlankRocketJump(thirtyTickWorld);
        var sixtyTickVerticalSpeed = TriggerPointBlankRocketJump(sixtyTickWorld);

        Assert.Equal(thirtyTickVerticalSpeed, sixtyTickVerticalSpeed, 3);
    }

    [Fact]
    public void RocketSelfBlast_UsesReducedOwnerSplashDamage()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 500f, 1000f, 40f);
        var wall = new LevelSolid(46f, 120f, 8f, 220f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, wall]));
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(20f, world.LocalPlayer.Y);
        var healthBeforeExplosion = world.LocalPlayer.Health;

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: 200f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var explosion = AdvanceUntilExplosionWithVisual(world);

        Assert.True(explosion.Exploded);
        var distance = DistanceBetween(
            explosion.X,
            explosion.Y,
            world.LocalPlayer.X,
            world.LocalPlayer.Y);
        var fullSplashDamage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
        var damageTaken = healthBeforeExplosion - world.LocalPlayer.Health;

        Assert.True(damageTaken > 0);
        Assert.True(damageTaken < (int)fullSplashDamage);
    }

    [Fact]
    public void MineDetonation_EmitsExplosionSoundAndVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Demoman);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 80f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        Assert.NotEmpty(world.Mines);

        world.DrainPendingSoundEvents();
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: true,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);

        var soundEvents = world.DrainPendingSoundEvents();
        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "ExplosionSnd");
        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "Explosion");
    }

    [Fact]
    public void SpyBackstab_EmitsBackstabVisual()
    {
        var world = CreateWorld();
        SetLocalClassAndRespawn(world, PlayerClass.Spy);

        Assert.True(world.LocalPlayer.TryToggleSpyCloak());
        world.DrainPendingVisualEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X + 32f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        var visualEvents = world.DrainPendingVisualEvents();

        Assert.Contains(visualEvents, visualEvent => visualEvent.EffectName == "BackstabRed");
    }

    [Fact]
    public void Jumping_EmitsJumpSound()
    {
        var world = CreateWorld();
        world.DrainPendingSoundEvents();

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: true,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: false,
            FireSecondary: false,
            AimWorldX: world.LocalPlayer.X,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();

        var soundEvents = world.DrainPendingSoundEvents();

        Assert.Contains(soundEvents, soundEvent => soundEvent.SoundName == "JumpSnd");
    }

    [Fact]
    public void IdlePlayerOnFlatGround_RemainsGroundedAcrossTicks()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 600f, 24f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor]));
        world.TeleportLocalPlayer(150f, floor.Top - (world.LocalPlayer.Height / 2f));

        for (var tick = 0; tick < 6; tick += 1)
        {
            world.SetLocalInput(default);
            world.AdvanceOneTick();
            Assert.True(world.LocalPlayer.IsGrounded, $"player lost grounded state on tick {tick}");
            Assert.Equal(0f, world.LocalPlayer.HorizontalSpeed);
        }
    }

    [Fact]
    public void JumpingIntoLowCeiling_DoesNotSnapPlayerBackToCorridorEntrance()
    {
        var world = CreateWorld();
        var floor = new LevelSolid(0f, 240f, 600f, 24f);
        var ceiling = new LevelSolid(160f, 170f, 260f, 18f);
        world.CombatTestSetLevel(CreateLevel(solids: [floor, ceiling]));
        world.TeleportLocalPlayer(150f, floor.Top - (world.LocalPlayer.Height / 2f));

        var previousX = world.LocalPlayer.X;
        for (var tick = 0; tick < 16; tick += 1)
        {
            world.SetLocalInput(new PlayerInputSnapshot(
                Left: false,
                Right: true,
                Up: tick == 0,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: world.LocalPlayer.X + 100f,
                AimWorldY: world.LocalPlayer.Y,
                DebugKill: false));
            world.AdvanceOneTick();
            Assert.True(world.LocalPlayer.X >= previousX - 0.1f);
            previousX = world.LocalPlayer.X;
        }

        Assert.True(world.LocalPlayer.X > ceiling.Left + 10f);
    }

    [Fact]
    public void ActiveSetupGate_RemainsBlockingDuringJumpAndWiggleMovement()
    {
        var world = CreateWorld();
        var gate = new RoomObjectMarker(RoomObjectType.ControlPointSetupGate, 300f, 200f, 60f, 6f, "setup-gate", SourceName: "setup-gate");
        var level = CreateLevel(mode: GameModeKind.ControlPoint, roomObjects: [gate], solids: [new LevelSolid(0f, 260f, 600f, 24f)]);
        world.CombatTestSetLevel(level);
        world.TeleportLocalPlayer(300f, 242f);

        Assert.True(world.ControlPointSetupActive);

        var minimumY = world.LocalPlayer.Y;
        var positions = new List<string>();
        for (var tick = 0; tick < 18; tick += 1)
        {
            world.SetLocalInput(new PlayerInputSnapshot(
                Left: tick % 2 == 0,
                Right: tick % 2 != 0,
                Up: tick == 0 || tick == 6 || tick == 12,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: world.LocalPlayer.X,
                AimWorldY: world.LocalPlayer.Y - 100f,
                DebugKill: false));
            world.AdvanceOneTick();
            minimumY = Math.Min(minimumY, world.LocalPlayer.Y);
            positions.Add($"{tick}:({world.LocalPlayer.X:F2},{world.LocalPlayer.Y:F2})");
        }

        Assert.True(
            minimumY >= gate.Bottom + (world.LocalPlayer.Height / 2f) - 0.1f,
            string.Join(" ", positions));
    }

    [Fact]
    public void AdditionalPlayableSlot_CanJoinSpawnAndReleaseCleanly()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));

        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        Assert.False(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.True(player.IsAlive);
        Assert.Equal(PlayerClass.Soldier, player.ClassId);
        Assert.Equal(PlayerTeam.Red, player.Team);
        Assert.Contains(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot && entry.Player.Id == player.Id);

        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.False(player.IsAlive);
        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot);
    }

    [Fact]
    public void AwaitingJoinSlots_AreExcludedFromActiveNetworkPlayers()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        world.PrepareLocalPlayerJoin();
        world.TryPrepareNetworkPlayerJoin(extraSlot);

        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == SimulationWorld.LocalPlayerSlot);
        Assert.DoesNotContain(world.EnumerateActiveNetworkPlayers(), entry => entry.Slot == extraSlot);
    }

    [Fact]
    public void ReleasingSlot_ResetsPlayerScoreboardStatsBeforeReuse()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.ApplyNetworkState(
            player.Team,
            CharacterClassCatalog.GetDefinition(player.ClassId),
            isAlive: true,
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.Health,
            player.CurrentShells,
            kills: 7,
            deaths: 3,
            caps: 2,
            healPoints: 40,
            metal: player.Metal,
            player.IsGrounded,
            player.IsCarryingIntel,
            player.IsSpyCloaked,
            player.SpyCloakAlpha,
            player.IsUbered,
            player.IsHeavyEating,
            player.HeavyEatTicksRemaining,
            player.IsSniperScoped,
            player.SniperChargeTicks,
            player.FacingDirectionX,
            player.AimDirectionDegrees,
            player.IsTaunting,
            player.TauntFrameIndex,
            player.IsChatBubbleVisible,
            player.ChatBubbleFrameIndex,
            player.ChatBubbleAlpha);

        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Soldier));

        Assert.Equal(0, player.Kills);
        Assert.Equal(0, player.Deaths);
        Assert.Equal(0, player.Caps);
        Assert.Equal(0, player.HealPoints);
    }

    [Fact]
    public void AdditionalPlayableSlot_CanCaptureEnemyIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;
        var ownBase = world.Level.GetIntelBase(PlayerTeam.Red);

        Assert.True(ownBase.HasValue);
        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        player.TeleportTo(ownBase.Value.X, ownBase.Value.Y);
        world.AdvanceOneTick();

        Assert.False(player.IsCarryingIntel);
        Assert.Equal(1, world.RedCaps);
        Assert.Equal(1, player.Caps);
        Assert.True(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void AdditionalPlayableSlot_DeathDropsCarriedIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        Assert.True(world.ForceKillNetworkPlayer(extraSlot));

        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void AdditionalPlayableSlot_CanManuallyDropCarriedIntel()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();

        Assert.True(player.IsCarryingIntel);

        Assert.True(world.TrySetNetworkPlayerInput(
            extraSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: true,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X,
                AimWorldY: player.Y,
                DebugKill: false)));

        world.AdvanceOneTick();

        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
    }

    [Fact]
    public void ReleasingAdditionalPlayableSlot_DropsCarriedIntelAndRemovesOwnedSentry()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Engineer));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        player.TeleportTo(world.BlueIntel.X, world.BlueIntel.Y);
        world.AdvanceOneTick();
        Assert.True(player.IsCarryingIntel);

        Assert.True(world.TrySetNetworkPlayerInput(
            extraSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: true,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: false,
                AimWorldX: player.X,
                AimWorldY: player.Y,
                DebugKill: false)));
        world.AdvanceOneTick();

        Assert.Single(world.Sentries);
        Assert.True(world.TryReleaseNetworkPlayerSlot(extraSlot));

        Assert.False(player.IsAlive);
        Assert.False(player.IsCarryingIntel);
        Assert.True(world.BlueIntel.IsDropped);
        Assert.False(world.BlueIntel.IsAtBase);
        Assert.Empty(world.Sentries);
    }

    [Fact]
    public void ReleasingAdditionalPlayableSlots_RemovesOwnedProjectiles()
    {
        var world = CreateWorld();
        world.CombatTestSetLevel(CreateLevel());
        const byte soldierSlot = 3;
        const byte medicSlot = 4;

        Assert.True(world.TryPrepareNetworkPlayerJoin(soldierSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(soldierSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(soldierSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(soldierSlot, out var soldier));

        Assert.True(world.TryPrepareNetworkPlayerJoin(medicSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(medicSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(medicSlot, PlayerClass.Medic));
        Assert.True(world.TryGetNetworkPlayer(medicSlot, out var medic));

        soldier.TeleportTo(320f, 220f);
        medic.TeleportTo(320f, 260f);

        Assert.True(world.TrySetNetworkPlayerInput(
            soldierSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: soldier.X + 220f,
                AimWorldY: soldier.Y - 20f,
                DebugKill: false)));
        Assert.True(world.TrySetNetworkPlayerInput(
            medicSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: false,
                FireSecondary: true,
                AimWorldX: medic.X + 220f,
                AimWorldY: medic.Y - 20f,
                DebugKill: false)));

        for (var tick = 0; tick < 3; tick += 1)
        {
            world.AdvanceOneTick();
        }

        Assert.NotEmpty(world.Rockets);
        Assert.NotEmpty(world.Needles);

        Assert.True(world.TryReleaseNetworkPlayerSlot(soldierSlot));
        Assert.True(world.TryReleaseNetworkPlayerSlot(medicSlot));

        Assert.Empty(world.Rockets);
        Assert.Empty(world.Needles);
    }

    [Fact]
    public void ArenaTeamCounts_IncludeAdditionalPlayableSlots()
    {
        var world = CreateWorld();
        const byte redExtraSlot = 3;
        const byte blueExtraSlot = 4;
        const byte blueExtraSlotTwo = 2;

        Assert.True(world.TryLoadLevel("arena_montane"));
        world.DespawnEnemyDummy();
        Assert.True(world.TrySetNetworkPlayerTeam(SimulationWorld.LocalPlayerSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(SimulationWorld.LocalPlayerSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(redExtraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(redExtraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(redExtraSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(blueExtraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(blueExtraSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(blueExtraSlot, PlayerClass.Scout));
        Assert.True(world.TryPrepareNetworkPlayerJoin(blueExtraSlotTwo));
        Assert.True(world.TrySetNetworkPlayerTeam(blueExtraSlotTwo, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(blueExtraSlotTwo, PlayerClass.Scout));

        Assert.Equal(2, world.ArenaRedPlayerCount);
        Assert.Equal(2, world.ArenaBluePlayerCount);
        Assert.Equal(2, world.ArenaRedAliveCount);
        Assert.Equal(2, world.ArenaBlueAliveCount);
    }

    [Fact]
    public void SentryTargetsAdditionalPlayableEnemy()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        SetLocalClassAndRespawn(world, PlayerClass.Engineer);
        Assert.True(world.TryBuildLocalSentry());
        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var player));

        var sentry = Assert.Single(world.Sentries);
        player.TeleportTo(sentry.X + 48f, sentry.Y);

        var acquiredTarget = false;
        for (var tick = 0; tick < 180; tick += 1)
        {
            world.AdvanceOneTick();
            if (sentry.CurrentTargetPlayerId == player.Id)
            {
                acquiredTarget = true;
            }

            if (player.Health < player.MaxHealth)
            {
                break;
            }
        }

        Assert.True(acquiredTarget);
        Assert.True(player.Health < player.MaxHealth);
    }

    [Fact]
    public void AdditionalPlayableMedic_CanHealAdditionalPlayableTeammate()
    {
        var world = CreateWorld();
        const byte medicSlot = 3;
        const byte teammateSlot = 5;

        Assert.True(world.TryPrepareNetworkPlayerJoin(medicSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(medicSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(medicSlot, PlayerClass.Medic));
        Assert.True(world.TryGetNetworkPlayer(medicSlot, out var medic));

        Assert.True(world.TryPrepareNetworkPlayerJoin(teammateSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(teammateSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(teammateSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(teammateSlot, out var teammate));

        teammate.TeleportTo(medic.X + 24f, medic.Y);
        teammate.ForceSetHealth(Math.Max(1, teammate.MaxHealth / 3));

        Assert.True(world.TrySetNetworkPlayerInput(
            medicSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: teammate.X,
                AimWorldY: teammate.Y,
                DebugKill: false)));

        var beganHealing = false;
        for (var tick = 0; tick < 10; tick += 1)
        {
            world.AdvanceOneTick();
            if (medic.IsMedicHealing && medic.MedicHealTargetId == teammate.Id)
            {
                beganHealing = true;
                break;
            }
        }

        Assert.True(beganHealing);
        Assert.Equal(teammate.Id, medic.MedicHealTargetId);
        Assert.True(teammate.Health > teammate.MaxHealth / 3);
        Assert.True(medic.MedicUberCharge > 0f);
    }

    [Fact]
    public void AdditionalPlayableSlot_RecordsOwnDeathCamState()
    {
        var world = CreateWorld();
        const byte victimSlot = 3;
        const byte killerSlot = 4;

        Assert.True(world.TryPrepareNetworkPlayerJoin(victimSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(victimSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(victimSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(victimSlot, out var victim));

        Assert.True(world.TryPrepareNetworkPlayerJoin(killerSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(killerSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(killerSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(killerSlot, out var killer));

        victim.TeleportTo(320f, 220f);
        killer.TeleportTo(360f, 220f);

        Assert.True(world.ForceKillNetworkPlayer(victimSlot));

        var deathCam = world.GetNetworkPlayerDeathCam(victimSlot);
        Assert.NotNull(deathCam);
        Assert.Equal(victim.X, deathCam!.FocusX);
        Assert.Equal("You were killed by the late", deathCam.KillMessage);
    }

    [Fact]
    public void EndedRound_HumiliatesLosersAndBlocksCombatInput()
    {
        var world = CreateWorld();
        const byte losingSlot = 3;

        Assert.True(world.TryLoadLevel("destroy"));
        Assert.True(world.TryPrepareNetworkPlayerJoin(losingSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(losingSlot, PlayerTeam.Blue));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(losingSlot, PlayerClass.Soldier));
        Assert.True(world.TryGetNetworkPlayer(losingSlot, out var losingPlayer));

        world.CombatTestSetGeneratorHealth(PlayerTeam.Blue, 4);
        Assert.True(world.CombatTestDamageGenerator(PlayerTeam.Blue, 10f));
        Assert.True(world.MatchState.IsEnded);
        Assert.Equal(PlayerTeam.Red, world.MatchState.WinnerTeam);
        Assert.True(world.IsPlayerHumiliated(losingPlayer));
        Assert.False(world.IsPlayerHumiliated(world.LocalPlayer));

        Assert.True(world.TrySetNetworkPlayerInput(
            losingSlot,
            new PlayerInputSnapshot(
                Left: false,
                Right: false,
                Up: false,
                Down: false,
                BuildSentry: false,
                DestroySentry: false,
                Taunt: false,
                FirePrimary: true,
                FireSecondary: false,
                AimWorldX: losingPlayer.X + 80f,
                AimWorldY: losingPlayer.Y,
                DebugKill: false)));

        world.AdvanceOneTick();

        Assert.Empty(world.Rockets);
    }

    [Fact]
    public void FullMapChange_ResetsPlayableSlotsToAwaitingJoin()
    {
        var world = CreateWorld();
        const byte extraSlot = 3;

        Assert.True(world.TryPrepareNetworkPlayerJoin(extraSlot));
        Assert.True(world.TrySetNetworkPlayerTeam(extraSlot, PlayerTeam.Red));
        Assert.True(world.TryApplyNetworkPlayerClassSelection(extraSlot, PlayerClass.Scout));
        Assert.True(world.TryGetNetworkPlayer(extraSlot, out var extraPlayer));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(SimulationWorld.LocalPlayerSlot));
        Assert.False(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.True(extraPlayer.IsAlive);

        Assert.True(world.TryLoadLevel("Waterway", mapAreaIndex: 1, preservePlayerStats: false));

        Assert.True(world.IsNetworkPlayerAwaitingJoin(SimulationWorld.LocalPlayerSlot));
        Assert.True(world.IsNetworkPlayerAwaitingJoin(extraSlot));
        Assert.False(world.LocalPlayer.IsAlive);
        Assert.False(extraPlayer.IsAlive);
    }

    private static SimulationWorld CreateWorld(int ticksPerSecond = SimulationConfig.DefaultTicksPerSecond)
    {
        return new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = ticksPerSecond,
        });
    }

    private static SimpleLevel CreateLevel(
        GameModeKind mode = GameModeKind.CaptureTheFlag,
        IReadOnlyList<RoomObjectMarker>? roomObjects = null,
        IReadOnlyList<LevelSolid>? solids = null)
    {
        var spawn = new SpawnPoint(100f, 220f);
        return new SimpleLevel(
            name: "SimulationTest",
            mode: mode,
            bounds: new WorldBounds(1000f, 600f),
            backgroundAssetName: null,
            mapAreaIndex: 1,
            mapAreaCount: 1,
            localSpawn: spawn,
            redSpawns: [spawn],
            blueSpawns: [new SpawnPoint(900f, 220f)],
            intelBases: Array.Empty<IntelBaseMarker>(),
            roomObjects: roomObjects ?? Array.Empty<RoomObjectMarker>(),
            floorY: 500f,
            solids: solids ?? Array.Empty<LevelSolid>(),
            importedFromSource: false);
    }

    [Fact]
    public void EnemyTrainingDummy_CanBeDisabledIndependently()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = SimulationConfig.DefaultTicksPerSecond,
            EnableLocalDummies = true,
            EnableEnemyTrainingDummy = false,
            EnableFriendlySupportDummy = true,
        });

        Assert.False(world.EnemyPlayerEnabled);
        world.SpawnEnemyDummy();
        Assert.False(world.EnemyPlayerEnabled);

        world.SpawnFriendlyDummy();
        Assert.True(world.FriendlyDummyEnabled);
    }

    [Fact]
    public void FriendlySupportDummy_CanBeDisabledIndependently()
    {
        var world = new SimulationWorld(new SimulationConfig
        {
            TicksPerSecond = SimulationConfig.DefaultTicksPerSecond,
            EnableLocalDummies = true,
            EnableEnemyTrainingDummy = true,
            EnableFriendlySupportDummy = false,
        });

        world.SpawnFriendlyDummy();
        Assert.False(world.FriendlyDummyEnabled);

        world.SpawnEnemyDummy();
        Assert.True(world.EnemyPlayerEnabled);
    }

    private static void AdvanceTicks(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick += 1)
        {
            world.AdvanceOneTick();
        }
    }

    private static void SetLocalClassAndRespawn(SimulationWorld world, PlayerClass playerClass)
    {
        Assert.True(world.TrySetLocalClass(playerClass));
        AdvanceTicks(world, 150);
        Assert.True(world.LocalPlayer.IsAlive);
        Assert.Equal(playerClass, world.LocalPlayer.ClassId);
    }

    private static float TriggerPointBlankRocketJump(SimulationWorld world)
    {
        SetLocalClassAndRespawn(world, PlayerClass.Soldier);
        world.TeleportLocalPlayer(30f, world.LocalPlayer.Y);

        world.SetLocalInput(new PlayerInputSnapshot(
            Left: false,
            Right: false,
            Up: false,
            Down: false,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: true,
            FireSecondary: false,
            AimWorldX: -100f,
            AimWorldY: world.LocalPlayer.Y,
            DebugKill: false));
        world.AdvanceOneTick();
        world.SetLocalInput(default);
        Assert.True(AdvanceUntilExplosion(world));
        return world.LocalPlayer.VerticalSpeed;
    }

    private static bool AdvanceUntilExplosion(SimulationWorld world, int maxTicks = 60)
    {
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            world.AdvanceOneTick();
            var soundEvents = world.DrainPendingSoundEvents();
            var visualEvents = world.DrainPendingVisualEvents();
            if (soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd")
                && visualEvents.Any(visualEvent => visualEvent.EffectName == "Explosion"))
            {
                return true;
            }
        }

        return false;
    }

    private static (bool Exploded, float X, float Y) AdvanceUntilExplosionWithVisual(SimulationWorld world, int maxTicks = 60)
    {
        for (var tick = 0; tick < maxTicks; tick += 1)
        {
            world.AdvanceOneTick();
            var soundEvents = world.DrainPendingSoundEvents();
            var visualEvents = world.DrainPendingVisualEvents();
            if (!soundEvents.Any(soundEvent => soundEvent.SoundName == "ExplosionSnd"))
            {
                continue;
            }

            var visual = visualEvents.FirstOrDefault(visualEvent => visualEvent.EffectName == "Explosion");
            if (visual is not null)
            {
                return (true, visual.X, visual.Y);
            }
        }

        return (false, 0f, 0f);
    }

    private static float DistanceBetween(float x1, float y1, float x2, float y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
