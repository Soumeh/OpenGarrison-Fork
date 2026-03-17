namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private readonly record struct ShotHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct FlameHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct RocketHitResult(float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct MineHitResult(float Distance, float HitX, float HitY, bool DestroyOnHit);
    private readonly record struct RifleHitResult(float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator);
    private readonly record struct RectangleHitbox(float Left, float Top, float Right, float Bottom);

    internal void CombatTestSetLevel(SimpleLevel level)
    {
        Level = level;
        MatchRules = CreateDefaultMatchRules(level.Mode);
        MatchState = CreateInitialMatchState(MatchRules);
        ResetModeStateForNewRound();
    }

    internal void CombatTestAddSentry(SentryEntity sentry)
    {
        _sentries.Add(sentry);
        _entities[sentry.Id] = sentry;
    }

    internal bool CombatTestHasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => Combat.HasLineOfSight(attacker, target);

    internal bool CombatTestHasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        => Combat.HasSentryLineOfSight(sentry, target);

    internal bool CombatTestHasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => Combat.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    internal bool CombatTestHasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => Combat.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    internal bool CombatTestIsFlameSpawnBlocked(PlayerEntity attacker, float spawnX, float spawnY)
        => Combat.IsFlameSpawnBlocked(attacker, spawnX, spawnY);

    internal static float? CombatTestGetLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance)
        => CombatResolver.GetLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance);

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestShotHit(
        ShotProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestShotHit(shot, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestNeedleHit(
        NeedleProjectileEntity needle,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestRevolverHit(
        RevolverProjectileEntity shot,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestBladeHit(
        BladeProjectileEntity blade,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestBladeHit(blade, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry)? CombatTestGetNearestStabHit(
        StabMaskEntity mask,
        float directionX,
        float directionY)
    {
        var hit = Combat.GetNearestStabHit(mask, directionX, directionY);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestRocketHit(
        RocketProjectileEntity rocket,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, float HitX, float HitY, bool DestroyOnHit)? CombatTestGetNearestMineHit(
        MineProjectileEntity mine,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestMineHit(mine, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.DestroyOnHit)
            : null;
    }

    internal (float Distance, float HitX, float HitY, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator)? CombatTestGetNearestFlameHit(
        FlameProjectileEntity flame,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.GetNearestFlameHit(flame, directionX, directionY, maxDistance);
        return hit.HasValue
            ? (hit.Value.Distance, hit.Value.HitX, hit.Value.HitY, hit.Value.HitPlayer, hit.Value.HitSentry, hit.Value.HitGenerator)
            : null;
    }

    internal (float Distance, PlayerEntity? HitPlayer, SentryEntity? HitSentry, GeneratorState? HitGenerator) CombatTestResolveRifleHit(
        PlayerEntity attacker,
        float directionX,
        float directionY,
        float maxDistance)
    {
        var hit = Combat.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
        return (hit.Distance, hit.HitPlayer, hit.HitSentry, hit.HitGenerator);
    }

    private bool HasLineOfSight(PlayerEntity attacker, PlayerEntity target)
        => Combat.HasLineOfSight(attacker, target);

    private bool HasSentryLineOfSight(SentryEntity sentry, PlayerEntity target)
        => Combat.HasSentryLineOfSight(sentry, target);

    private bool HasDirectLineOfSight(float originX, float originY, float targetX, float targetY, PlayerTeam targetTeam)
        => Combat.HasDirectLineOfSight(originX, originY, targetX, targetY, targetTeam);

    private bool HasObstacleLineOfSight(float originX, float originY, float targetX, float targetY)
        => Combat.HasObstacleLineOfSight(originX, originY, targetX, targetY);

    private bool IsFlameSpawnBlocked(PlayerEntity attacker, float spawnX, float spawnY)
        => Combat.IsFlameSpawnBlocked(attacker, spawnX, spawnY);

    private static float? GetLineIntersectionDistanceToPlayer(
        float originX,
        float originY,
        float endX,
        float endY,
        PlayerEntity player,
        float maxDistance)
        => CombatResolver.GetLineIntersectionDistanceToPlayer(originX, originY, endX, endY, player, maxDistance);

    private ShotHitResult? GetNearestShotHit(ShotProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestShotHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestNeedleHit(NeedleProjectileEntity needle, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestNeedleHit(needle, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestRevolverHit(RevolverProjectileEntity shot, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestRevolverHit(shot, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestBladeHit(BladeProjectileEntity blade, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestBladeHit(blade, directionX, directionY, maxDistance);

    private ShotHitResult? GetNearestStabHit(StabMaskEntity mask, float directionX, float directionY)
        => Combat.GetNearestStabHit(mask, directionX, directionY);

    private RocketHitResult? GetNearestRocketHit(RocketProjectileEntity rocket, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestRocketHit(rocket, directionX, directionY, maxDistance);

    private MineHitResult? GetNearestMineHit(MineProjectileEntity mine, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestMineHit(mine, directionX, directionY, maxDistance);

    private FlameHitResult? GetNearestFlameHit(FlameProjectileEntity flame, float directionX, float directionY, float maxDistance)
        => Combat.GetNearestFlameHit(flame, directionX, directionY, maxDistance);

    private RifleHitResult ResolveRifleHit(PlayerEntity attacker, float directionX, float directionY, float maxDistance)
        => Combat.ResolveRifleHit(attacker, directionX, directionY, maxDistance);
}
