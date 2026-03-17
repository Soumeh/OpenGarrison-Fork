namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private const float PyroAirblastDistance = 150f;
    private const float PyroAirblastHalfAngleDegrees = 37.5f;
    private const float PyroAirblastRocketSpeed = 12f;
    private const float PyroAirblastMineImpulse = 8f;
    private const float PyroAirblastPlayerImpulse = 180f;
    private const float PyroAirblastPlayerLift = -120f;

    private void TriggerPyroAirblast(PlayerEntity player, float aimWorldX, float aimWorldY)
    {
        var aimDegrees = PointDirectionDegrees(player.X, player.Y, aimWorldX, aimWorldY);
        var aimRadians = DegreesToRadians(aimDegrees);
        var originX = player.X + MathF.Cos(aimRadians) * 25f;
        var originY = player.Y + MathF.Sin(aimRadians) * 25f;

        RegisterSoundEvent(player, "CompressionBlastSnd");
        RegisterVisualEffect("AirBlast", originX, originY, aimDegrees);

        DestroyFriendlyFlamesInAirblast(player.Team, originX, originY, aimDegrees);
        ReflectEnemyRockets(player, aimDegrees, originX, originY);
        PushEnemyMines(player.Team, originX, originY, aimDegrees);
        PushEnemyPlayers(player, originX, originY, aimDegrees);
        PushLooseBodies(originX, originY, aimDegrees);
    }

    private void DestroyFriendlyFlamesInAirblast(PlayerTeam team, float originX, float originY, float aimDegrees)
    {
        for (var flameIndex = _flames.Count - 1; flameIndex >= 0; flameIndex -= 1)
        {
            var flame = _flames[flameIndex];
            if (flame.Team != team || !IsWithinAirblastCone(originX, originY, flame.X, flame.Y, aimDegrees))
            {
                continue;
            }

            RemoveFlameAt(flameIndex);
        }
    }

    private void ReflectEnemyRockets(PlayerEntity player, float aimDegrees, float originX, float originY)
    {
        var aimRadians = DegreesToRadians(aimDegrees);
        for (var rocketIndex = 0; rocketIndex < _rockets.Count; rocketIndex += 1)
        {
            var rocket = _rockets[rocketIndex];
            if (rocket.Team == player.Team || !IsWithinAirblastCone(originX, originY, rocket.X, rocket.Y, aimDegrees))
            {
                continue;
            }

            rocket.Reflect(player.Id, player.Team, aimRadians, PyroAirblastRocketSpeed);
        }
    }

    private void PushEnemyMines(PlayerTeam team, float originX, float originY, float aimDegrees)
    {
        var aimRadians = DegreesToRadians(aimDegrees);
        var impulseX = MathF.Cos(aimRadians) * PyroAirblastMineImpulse;
        var impulseY = MathF.Sin(aimRadians) * PyroAirblastMineImpulse;
        for (var mineIndex = 0; mineIndex < _mines.Count; mineIndex += 1)
        {
            var mine = _mines[mineIndex];
            if (mine.Team == team || !IsWithinAirblastCone(originX, originY, mine.X, mine.Y, aimDegrees))
            {
                continue;
            }

            mine.Unstick();
            mine.ApplyImpulse(impulseX, impulseY);
        }
    }

    private void PushEnemyPlayers(PlayerEntity player, float originX, float originY, float aimDegrees)
    {
        var aimRadians = DegreesToRadians(aimDegrees);
        for (var index = 0; index < NetworkPlayerSlots.Count; index += 1)
        {
            if (!TryGetNetworkPlayer(NetworkPlayerSlots[index], out var target)
                || !target.IsAlive
                || target.Team == player.Team
                || target.Id == player.Id
                || !IsWithinAirblastCone(originX, originY, target.X, target.Y, aimDegrees))
            {
                continue;
            }

            var distance = Math.Max(1f, DistanceBetween(originX, originY, target.X, target.Y));
            var scale = 1f - (distance / PyroAirblastDistance);
            target.AddImpulse(
                MathF.Cos(aimRadians) * PyroAirblastPlayerImpulse * scale,
                MathF.Sin(aimRadians) * PyroAirblastPlayerImpulse * scale + PyroAirblastPlayerLift * scale);
            target.SetMovementState(LegacyMovementState.Airblast);
        }

        if (EnemyPlayerEnabled
            && EnemyPlayer.IsAlive
            && EnemyPlayer.Team != player.Team
            && EnemyPlayer.Id != player.Id
            && IsWithinAirblastCone(originX, originY, EnemyPlayer.X, EnemyPlayer.Y, aimDegrees))
        {
            var distance = Math.Max(1f, DistanceBetween(originX, originY, EnemyPlayer.X, EnemyPlayer.Y));
            var scale = 1f - (distance / PyroAirblastDistance);
            EnemyPlayer.AddImpulse(
                MathF.Cos(aimRadians) * PyroAirblastPlayerImpulse * scale,
                MathF.Sin(aimRadians) * PyroAirblastPlayerImpulse * scale + PyroAirblastPlayerLift * scale);
            EnemyPlayer.SetMovementState(LegacyMovementState.Airblast);
        }
    }

    private void PushLooseBodies(float originX, float originY, float aimDegrees)
    {
        var aimRadians = DegreesToRadians(aimDegrees);
        foreach (var body in _deadBodies)
        {
            if (!IsWithinAirblastCone(originX, originY, body.X, body.Y, aimDegrees))
            {
                continue;
            }

            body.AddImpulse(MathF.Cos(aimRadians) * 6f, MathF.Sin(aimRadians) * 6f);
        }

        foreach (var gib in _playerGibs)
        {
            if (!IsWithinAirblastCone(originX, originY, gib.X, gib.Y, aimDegrees))
            {
                continue;
            }

            gib.AddImpulse(MathF.Cos(aimRadians) * 6f, MathF.Sin(aimRadians) * 6f, 0f);
        }
    }

    private static float GetAirblastAngleDelta(float fromDegrees, float toDegrees)
    {
        var delta = NormalizeAngleDegrees(toDegrees - fromDegrees);
        if (delta > 180f)
        {
            delta -= 360f;
        }

        return MathF.Abs(delta);
    }

    private static bool IsWithinAirblastCone(float originX, float originY, float targetX, float targetY, float aimDegrees)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance > PyroAirblastDistance)
        {
            return false;
        }

        var targetDegrees = PointDirectionDegrees(originX, originY, targetX, targetY);
        return GetAirblastAngleDelta(aimDegrees, targetDegrees) <= PyroAirblastHalfAngleDegrees;
    }
}
