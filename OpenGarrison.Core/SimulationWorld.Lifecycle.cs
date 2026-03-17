namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private void KillPlayer(
        PlayerEntity player,
        bool gibbed = false,
        PlayerEntity? killer = null,
        string? weaponSpriteName = null,
        string? deathCamMessage = null,
        SentryEntity? deathCamSentry = null,
        string? killFeedMessage = null,
        bool createDeathCam = true)
    {
        player.AddDeath();
        if (killer is not null && !ReferenceEquals(killer, player))
        {
            killer.AddKill();
        }

        if (player.IsCarryingIntel)
        {
            GetEnemyIntelState(player.Team).Drop(
                player.X,
                player.Y,
                IntelReturnTicks);
            player.DropIntel(IntelPickupCooldownTicksAfterDrop);
            RegisterWorldSoundEvent("IntelDropSnd", player.X, player.Y);
        }

        if (gibbed)
        {
            SpawnPlayerGibs(player);
            RegisterWorldSoundEvent("Gibbing", player.X, player.Y);
        }
        else
        {
            SpawnDeadBody(player);
            RegisterWorldSoundEvent(_random.Next(2) == 0 ? "DeathSnd1" : "DeathSnd2", player.X, player.Y);
        }

        RecordKillFeedEntry(player, killer, weaponSpriteName ?? "DeadS", killFeedMessage);
        var respawnTicks = MatchRules.Mode == GameModeKind.Arena
            ? 0
            : player.IsInSpawnRoom
                ? 1
                : _configuredRespawnTicks;
        var hasNetworkSlot = TryGetNetworkPlayerSlot(player, out var slot);

        if (createDeathCam && hasNetworkSlot)
        {
            var deathCamTicks = Math.Clamp(respawnTicks > 0 ? respawnTicks : _configuredRespawnTicks, 1, 150);
            LocalDeathCamState deathCam;
            if (deathCamSentry is not null)
            {
                deathCam = new LocalDeathCamState(
                    deathCamSentry.X,
                    deathCamSentry.Y,
                    deathCamMessage ?? "You were killed by the autogun of",
                    killer?.DisplayName ?? string.Empty,
                    killer?.Team,
                    deathCamSentry.Health,
                    SentryEntity.MaxHealth,
                    deathCamTicks,
                    deathCamTicks);
            }
            else if (killer is not null)
            {
                deathCam = new LocalDeathCamState(
                    killer.X,
                    killer.Y,
                    deathCamMessage ?? "You were killed by",
                    killer.DisplayName,
                    killer.Team,
                    killer.Health,
                    killer.MaxHealth,
                    deathCamTicks,
                    deathCamTicks);
            }
            else
            {
                deathCam = new LocalDeathCamState(
                    player.X,
                    player.Y,
                    deathCamMessage ?? "You were killed by the late",
                    string.Empty,
                    null,
                    0,
                    0,
                    deathCamTicks,
                    deathCamTicks);
            }

            SetNetworkPlayerDeathCam(slot, deathCam);
        }

        RemoveOwnedSpyArtifacts(player.Id);
        player.Kill();
        if (hasNetworkSlot)
        {
            TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }
        else if (ReferenceEquals(player, EnemyPlayer))
        {
            _enemyDummyRespawnTicks = respawnTicks;
        }

        foreach (var otherPlayer in EnumerateSimulatedPlayers())
        {
            if (otherPlayer.MedicHealTargetId == player.Id)
            {
                otherPlayer.ClearMedicHealingTarget();
            }
        }
    }

    private void AdvanceLocalDeathCam()
    {
        if (LocalDeathCam is null)
        {
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        if (LocalDeathCam.RemainingTicks <= 1)
        {
            LocalDeathCam = null;
            AdvanceAdditionalNetworkDeathCams();
            return;
        }

        LocalDeathCam = LocalDeathCam with { RemainingTicks = LocalDeathCam.RemainingTicks - 1 };
        AdvanceAdditionalNetworkDeathCams();
    }

    private void AdvanceAdditionalNetworkDeathCams()
    {
        if (_networkPlayerDeathCams.Count == 0)
        {
            return;
        }

        var staleSlots = new List<byte>();
        foreach (var entry in _networkPlayerDeathCams)
        {
            if (entry.Value.RemainingTicks <= 1)
            {
                staleSlots.Add(entry.Key);
                continue;
            }

            _networkPlayerDeathCams[entry.Key] = entry.Value with { RemainingTicks = entry.Value.RemainingTicks - 1 };
        }

        for (var index = 0; index < staleSlots.Count; index += 1)
        {
            _networkPlayerDeathCams.Remove(staleSlots[index]);
        }
    }

    private void SetNetworkPlayerDeathCam(byte slot, LocalDeathCamState? deathCam)
    {
        if (slot == LocalPlayerSlot)
        {
            LocalDeathCam = deathCam;
            return;
        }

        if (deathCam is null)
        {
            _networkPlayerDeathCams.Remove(slot);
            return;
        }

        _networkPlayerDeathCams[slot] = deathCam;
    }

    private void AdvanceKillFeed()
    {
        if (_killFeed.Count == 0)
        {
            _killFeedTrimTicks = 0;
            return;
        }

        if (_killFeedTrimTicks > 0)
        {
            _killFeedTrimTicks -= 1;
        }

        if (_killFeedTrimTicks > 0)
        {
            return;
        }

        _killFeed.RemoveAt(0);
        _killFeedTrimTicks = _killFeed.Count > 0 ? KillFeedLifetimeTicks : 0;
    }

    private void RecordKillFeedEntry(PlayerEntity victim, PlayerEntity? killer, string weaponSpriteName, string? messageText = null)
    {
        var entry = killer is null
            ? new KillFeedEntry(string.Empty, victim.Team, weaponSpriteName, victim.DisplayName, victim.Team, messageText ?? string.Empty)
            : new KillFeedEntry(killer.DisplayName, killer.Team, weaponSpriteName, victim.DisplayName, victim.Team, messageText ?? string.Empty);
        _killFeed.Add(entry);
        if (_killFeed.Count > 5)
        {
            _killFeed.RemoveAt(0);
        }

        _killFeedTrimTicks = KillFeedLifetimeTicks;
    }

    private static string GetKillFeedWeaponSprite(PlayerEntity? attacker)
    {
        if (attacker is null)
        {
            return "DeadS";
        }

        return attacker.PrimaryWeapon.Kind switch
        {
            PrimaryWeaponKind.Medigun => "MedigunS",
            PrimaryWeaponKind.Rifle => "RifleS",
            PrimaryWeaponKind.MineLauncher => "MinegunS",
            PrimaryWeaponKind.Minigun => "MinigunS",
            PrimaryWeaponKind.FlameThrower => "FlamethrowerS",
            PrimaryWeaponKind.RocketLauncher => "RocketlauncherS",
            PrimaryWeaponKind.Revolver => "RevolverS",
            PrimaryWeaponKind.PelletGun => attacker.ClassId == PlayerClass.Engineer ? "ShotgunS" : "ScattergunS",
            _ => "DeadS",
        };
    }

    private void AdvanceNetworkRespawnTimer(byte slot)
    {
        if (IsNetworkPlayerAwaitingJoin(slot)
            || !TryGetNetworkPlayer(slot, out var player))
        {
            return;
        }

        if (MatchRules.Mode == GameModeKind.Arena && !MatchState.IsEnded)
        {
            return;
        }

        var respawnTicks = GetNetworkPlayerRespawnTicks(slot);
        if (respawnTicks > 0)
        {
            respawnTicks -= 1;
            TrySetNetworkPlayerRespawnTicks(slot, respawnTicks);
        }

        if (respawnTicks > 0)
        {
            return;
        }

        var team = GetNetworkPlayerConfiguredTeam(slot);
        var spawn = ReserveSpawn(player, team);
        player.SetClassDefinition(GetNetworkPlayerClassDefinition(slot));
        player.Spawn(team, spawn.X, spawn.Y);
    }

    private void AdvanceEnemyDummyRespawnTimer()
    {
        if (!EnemyPlayerEnabled)
        {
            return;
        }

        if (MatchRules.Mode == GameModeKind.Arena && !MatchState.IsEnded)
        {
            return;
        }

        if (_enemyDummyRespawnTicks > 0)
        {
            _enemyDummyRespawnTicks -= 1;
        }

        if (_enemyDummyRespawnTicks > 0)
        {
            return;
        }

        var spawn = ReserveSpawn(EnemyPlayer, _enemyDummyTeam);
        EnemyPlayer.SetClassDefinition(_enemyDummyClassDefinition);
        EnemyPlayer.Spawn(_enemyDummyTeam, spawn.X, spawn.Y);
        EnemyPlayer.ClearMedicHealingTarget();
    }

    private void SpawnDeadBody(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return;
        }

        var deadBody = new DeadBodyEntity(
            AllocateEntityId(),
            player.ClassId,
            player.Team,
            player.X,
            player.Y,
            player.Width,
            player.Height,
            player.HorizontalSpeed * (float)Config.FixedDeltaSeconds,
            player.VerticalSpeed * (float)Config.FixedDeltaSeconds,
            MathF.Cos(player.AimDirectionDegrees * (MathF.PI / 180f)) < 0f);
        _deadBodies.Add(deadBody);
        _entities.Add(deadBody.Id, deadBody);
    }
}
