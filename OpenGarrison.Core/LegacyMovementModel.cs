using System;

namespace OpenGarrison.Core;

public enum LegacyMovementState : byte
{
    None = 0,
    ExplosionRecovery = 1,
    RocketJuggle = 2,
    Airblast = 3,
}

public static class LegacyMovementModel
{
    public const float SourceTicksPerSecond = 30f;
    public const float JumpStrengthToJumpSpeed = SourceTicksPerSecond;
    public const float GravityPerTick = 0.6f;
    public const float RisingBlastLiftPerTick = -0.1f;
    public const float MaxFallSpeedPerTick = 10f;
    public const float StopSpeedThresholdPerTick = 0.2f;
    public const float IntelSpeedFactorCap = 0.75f;

    private const float NormalSpeedFactor = 0.85f;
    private const float ExplosionRecoverySpeedFactor = 0.65f;
    private const float RocketJuggleSpeedFactor = 0.17f;
    private const float AirblastSpeedFactor = 0.1f;

    private const float NormalHorizontalDivisor = 1.15f;
    private const float RocketJuggleHorizontalDivisor = 1.04f;
    private const float AirblastHorizontalDivisor = 1.002f;

    public static float GetGravityPerSecondSquared()
    {
        return GravityPerTick * SourceTicksPerSecond * SourceTicksPerSecond;
    }

    public static float GetJumpSpeed(float jumpStrength)
    {
        return jumpStrength * JumpStrengthToJumpSpeed;
    }

    public static float GetMaxRunSpeed(float runPower)
    {
        var steadyStatePerTick = (runPower * NormalSpeedFactor / NormalHorizontalDivisor) / (1f - (1f / NormalHorizontalDivisor));
        return steadyStatePerTick * SourceTicksPerSecond;
    }

    public static float GetContinuousRunDrive(float runPower)
    {
        var divisor = NormalHorizontalDivisor;
        var decayPerSourceTick = 1f / divisor;
        var drivePerSecond = (runPower * NormalSpeedFactor * SourceTicksPerSecond) / divisor;
        return drivePerSecond * GetDecayRatio(divisor) / (1f - decayPerSourceTick);
    }

    public static float AdvanceHorizontalSpeed(
        float currentSpeed,
        float runPower,
        float movementScale,
        float horizontalDirection,
        LegacyMovementState state,
        bool isCarryingIntel,
        float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return currentSpeed;
        }

        var divisor = GetHorizontalDivisor(state);
        var decayPerSourceTick = 1f / divisor;
        var decay = MathF.Pow(decayPerSourceTick, SourceTicksPerSecond * deltaSeconds);
        var speedFactor = GetSpeedFactor(state);
        if (isCarryingIntel && speedFactor > IntelSpeedFactorCap)
        {
            speedFactor = IntelSpeedFactorCap;
        }

        var nextSpeed = currentSpeed * decay;
        if (horizontalDirection != 0f && movementScale > 0f)
        {
            var perSourceTickInput = horizontalDirection * runPower * movementScale * speedFactor;
            var sourceTickContribution = (perSourceTickInput * SourceTicksPerSecond) / divisor;
            nextSpeed += sourceTickContribution * GetDecayBlend(decay, divisor);
        }

        return MathF.Abs(nextSpeed) < StopSpeedThresholdPerTick * SourceTicksPerSecond
            ? 0f
            : nextSpeed;
    }

    public static float AdvanceVerticalSpeed(
        float currentSpeed,
        bool isAirborne,
        float deltaSeconds,
        ref LegacyMovementState state)
    {
        if (state != LegacyMovementState.None && currentSpeed >= 0f)
        {
            state = LegacyMovementState.None;
        }

        if (deltaSeconds <= 0f || !isAirborne)
        {
            return currentSpeed;
        }

        var gravityPerSecondSquared = GetGravityPerSecondSquared();
        if (state != LegacyMovementState.None && currentSpeed < 0f)
        {
            gravityPerSecondSquared += RisingBlastLiftPerTick * SourceTicksPerSecond * SourceTicksPerSecond;
        }

        var nextSpeed = currentSpeed + gravityPerSecondSquared * deltaSeconds;
        return MathF.Min(nextSpeed, MaxFallSpeedPerTick * SourceTicksPerSecond);
    }

    private static float GetDecayBlend(float decay, float divisor)
    {
        var decayPerSourceTick = 1f / divisor;
        return (1f - decay) / (1f - decayPerSourceTick);
    }

    private static float GetDecayRatio(float divisor)
    {
        return SourceTicksPerSecond * MathF.Log(divisor);
    }

    private static float GetSpeedFactor(LegacyMovementState state)
    {
        return state switch
        {
            LegacyMovementState.ExplosionRecovery => ExplosionRecoverySpeedFactor,
            LegacyMovementState.RocketJuggle => RocketJuggleSpeedFactor,
            LegacyMovementState.Airblast => AirblastSpeedFactor,
            _ => NormalSpeedFactor,
        };
    }

    private static float GetHorizontalDivisor(LegacyMovementState state)
    {
        return state switch
        {
            LegacyMovementState.RocketJuggle => RocketJuggleHorizontalDivisor,
            LegacyMovementState.Airblast => AirblastHorizontalDivisor,
            _ => NormalHorizontalDivisor,
        };
    }
}
