namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{

    private void ExplodeRocket(RocketProjectileEntity rocket, PlayerEntity? directHitPlayer, SentryEntity? directHitSentry, GeneratorState? directHitGenerator)
    {
        for (var rocketIndex = _rockets.Count - 1; rocketIndex >= 0; rocketIndex -= 1)
        {
            if (_rockets[rocketIndex].Id == rocket.Id)
            {
                RemoveRocketAt(rocketIndex);
                break;
            }
        }

        if (directHitPlayer is not null && !ReferenceEquals(directHitPlayer, FindPlayerById(rocket.OwnerId)))
        {
            if (directHitPlayer.ApplyDamage(RocketProjectileEntity.DirectHitDamage, PlayerEntity.SpyDamageRevealAlpha))
            {
                KillPlayer(directHitPlayer, gibbed: true, killer: FindPlayerById(rocket.OwnerId), weaponSpriteName: "RocketlauncherS");
            }
        }

        if (directHitSentry is not null)
        {
            directHitSentry.ApplyDamage(RocketProjectileEntity.DirectHitDamage);
            if (directHitSentry.Health <= 0)
            {
                DestroySentry(directHitSentry);
            }
        }

        if (directHitGenerator is not null)
        {
            TryDamageGenerator(directHitGenerator.Team, RocketProjectileEntity.DirectHitDamage);
        }

        RegisterWorldSoundEvent("ExplosionSnd", rocket.X, rocket.Y);
        RegisterVisualEffect("Explosion", rocket.X, rocket.Y);
        ApplyDeadBodyExplosionImpulse(rocket.X, rocket.Y, RocketProjectileEntity.BlastRadius, 10f);
        ApplyPlayerGibExplosionImpulse(rocket.X, rocket.Y, RocketProjectileEntity.BlastRadius, 15f);
        RegisterExplosionTraces(rocket.X, rocket.Y);

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = DistanceBetween(rocket.X, rocket.Y, player.X, player.Y);
            if (distance >= RocketProjectileEntity.BlastRadius)
            {
                continue;
            }

            var distanceFactor = 1f - (distance / RocketProjectileEntity.BlastRadius);
            var damage = RocketProjectileEntity.ExplosionDamage * distanceFactor;
            var impulse = RocketProjectileEntity.Knockback * distanceFactor * LegacyMovementModel.SourceTicksPerSecond;

            ApplyExplosionImpulse(player, rocket.X, rocket.Y, impulse);
            if (player.Id == rocket.OwnerId && player.Team == rocket.Team)
            {
                player.SetMovementState(LegacyMovementState.ExplosionRecovery);
            }
            else if (player.Team != rocket.Team)
            {
                player.SetMovementState(LegacyMovementState.RocketJuggle);
            }

            if (player.Team != rocket.Team || player.Id == rocket.OwnerId)
            {
                var appliedDamage = player.Id == rocket.OwnerId && player.Team == rocket.Team
                    ? damage * (2f / 3f)
                    : damage;
                RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(rocket.X, rocket.Y, player.X, player.Y) - 180f, 3);
                if (player.ApplyContinuousDamage(appliedDamage, PlayerEntity.SpyDamageRevealAlpha))
                {
                    KillPlayer(player, gibbed: true, killer: FindPlayerById(rocket.OwnerId), weaponSpriteName: "RocketlauncherS");
                }
            }
        }

        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(rocket.X, rocket.Y, sentry.X, sentry.Y);
            if (distance >= RocketProjectileEntity.BlastRadius || sentry.Team == rocket.Team)
            {
                continue;
            }

            var damage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
            if (sentry.ApplyDamage((int)MathF.Ceiling(damage)))
            {
                DestroySentry(sentry);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _generators.Count; generatorIndex += 1)
        {
            var generator = _generators[generatorIndex];
            var distance = DistanceBetween(rocket.X, rocket.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= RocketProjectileEntity.BlastRadius || generator.Team == rocket.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damage = RocketProjectileEntity.ExplosionDamage * (1f - (distance / RocketProjectileEntity.BlastRadius));
            TryDamageGenerator(generator.Team, damage);
        }
    }

    private static void ApplyExplosionImpulse(PlayerEntity player, float originX, float originY, float impulse)
    {
        if (impulse <= 0.0001f)
        {
            return;
        }

        var deltaX = player.X - originX;
        var deltaY = player.Y - originY;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= 0.0001f)
        {
            player.AddImpulse(0f, -impulse);
            return;
        }

        player.AddImpulse((deltaX / distance) * impulse, (deltaY / distance) * impulse);
    }

    private void RegisterExplosionTraces(float centerX, float centerY)
    {
        const int traceCount = 8;
        for (var index = 0; index < traceCount; index += 1)
        {
            var angle = (MathF.PI * 2f * index) / traceCount;
            RegisterCombatTrace(
                centerX,
                centerY,
                MathF.Cos(angle),
                MathF.Sin(angle),
                RocketProjectileEntity.BlastRadius * 0.5f,
                true);
        }
    }

    private void DetonateOwnedMines(int ownerId)
    {
        var queuedMineIds = new Queue<int>();
        foreach (var mine in _mines)
        {
            if (mine.OwnerId == ownerId)
            {
                queuedMineIds.Enqueue(mine.Id);
            }
        }

        while (queuedMineIds.Count > 0)
        {
            var mineId = queuedMineIds.Dequeue();
            var mine = FindMineById(mineId);
            if (mine is null)
            {
                continue;
            }

            foreach (var chainedMine in GetTriggeredMines(mine))
            {
                queuedMineIds.Enqueue(chainedMine.Id);
            }

            ExplodeMine(mine);
        }
    }

    private void ExplodeMine(MineProjectileEntity mine)
    {
        for (var mineIndex = _mines.Count - 1; mineIndex >= 0; mineIndex -= 1)
        {
            if (_mines[mineIndex].Id == mine.Id)
            {
                RemoveMineAt(mineIndex);
                break;
            }
        }

        RegisterWorldSoundEvent("ExplosionSnd", mine.X, mine.Y);
        RegisterVisualEffect("Explosion", mine.X, mine.Y);
        ApplyDeadBodyExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.BlastRadius, 10f);
        ApplyPlayerGibExplosionImpulse(mine.X, mine.Y, MineProjectileEntity.BlastRadius, 15f);
        RegisterExplosionTraces(mine.X, mine.Y);

        foreach (var player in EnumerateSimulatedPlayers())
        {
            if (!player.IsAlive)
            {
                continue;
            }

            var distance = DistanceBetween(mine.X, mine.Y, player.X, player.Y);
            if (distance >= MineProjectileEntity.BlastRadius)
            {
                continue;
            }

            var factor = 1f - (distance / MineProjectileEntity.BlastRadius);
            ApplyExplosionImpulse(player, mine.X, mine.Y, MineProjectileEntity.BlastImpulse * factor * LegacyMovementModel.SourceTicksPerSecond);
            if (player.Id == mine.OwnerId && player.Team == mine.Team)
            {
                player.SetMovementState(LegacyMovementState.ExplosionRecovery);
                player.ScaleVerticalSpeed(0.8f);
            }

            if (player.Team != mine.Team || player.Id == mine.OwnerId)
            {
                RegisterBloodEffect(player.X, player.Y, PointDirectionDegrees(mine.X, mine.Y, player.X, player.Y) - 180f, 3);
                if (player.ApplyContinuousDamage(mine.ExplosionDamage * factor, PlayerEntity.SpyMineRevealAlpha))
                {
                    KillPlayer(player, gibbed: true, killer: FindPlayerById(mine.OwnerId), weaponSpriteName: "MinegunS");
                }
            }
        }

        for (var sentryIndex = _sentries.Count - 1; sentryIndex >= 0; sentryIndex -= 1)
        {
            var sentry = _sentries[sentryIndex];
            var distance = DistanceBetween(mine.X, mine.Y, sentry.X, sentry.Y);
            if (distance >= MineProjectileEntity.BlastRadius || sentry.Team == mine.Team)
            {
                continue;
            }

            var damage = mine.ExplosionDamage * (1f - (distance / MineProjectileEntity.BlastRadius));
            if (sentry.ApplyDamage((int)MathF.Ceiling(damage)))
            {
                DestroySentry(sentry);
            }
        }

        for (var generatorIndex = 0; generatorIndex < _generators.Count; generatorIndex += 1)
        {
            var generator = _generators[generatorIndex];
            var distance = DistanceBetween(mine.X, mine.Y, generator.Marker.CenterX, generator.Marker.CenterY);
            if (distance >= MineProjectileEntity.BlastRadius || generator.Team == mine.Team || generator.IsDestroyed)
            {
                continue;
            }

            var damage = mine.ExplosionDamage * (1f - (distance / MineProjectileEntity.BlastRadius));
            TryDamageGenerator(generator.Team, damage);
        }
    }

    private MineProjectileEntity? FindMineById(int mineId)
    {
        foreach (var mine in _mines)
        {
            if (mine.Id == mineId)
            {
                return mine;
            }
        }

        return null;
    }

    private IEnumerable<MineProjectileEntity> GetTriggeredMines(MineProjectileEntity sourceMine)
    {
        foreach (var mine in _mines)
        {
            if (mine.Id == sourceMine.Id)
            {
                continue;
            }

            var distance = DistanceBetween(sourceMine.X, sourceMine.Y, mine.X, mine.Y);
            if (distance >= MineProjectileEntity.BlastRadius * 0.66f)
            {
                continue;
            }

            if (mine.Team != sourceMine.Team || mine.OwnerId == sourceMine.OwnerId)
            {
                yield return mine;
            }
        }
    }

    private void ApplyDeadBodyExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse)
    {
        foreach (var deadBody in _deadBodies)
        {
            var distance = DistanceBetween(originX, originY, deadBody.X, deadBody.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / blastRadius);
            var angle = MathF.Atan2(deadBody.Y - originY, deadBody.X - originX);
            deadBody.AddImpulse(MathF.Cos(angle) * maxImpulse * impulseScale, MathF.Sin(angle) * maxImpulse * impulseScale);
        }
    }

    private void ApplyPlayerGibExplosionImpulse(float originX, float originY, float blastRadius, float maxImpulse)
    {
        foreach (var gib in _playerGibs)
        {
            var distance = DistanceBetween(originX, originY, gib.X, gib.Y);
            if (distance >= blastRadius)
            {
                continue;
            }

            var impulseScale = 1f - (distance / blastRadius);
            var angle = MathF.Atan2(gib.Y - originY, gib.X - originX);
            gib.AddImpulse(
                MathF.Cos(angle) * maxImpulse * impulseScale,
                MathF.Sin(angle) * maxImpulse * impulseScale,
                ((_random.NextSingle() * 151f) - 75f) * impulseScale);
        }
    }
}

