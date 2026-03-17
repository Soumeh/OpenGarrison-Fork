using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class LegacyMovementModelTests
{
    [Fact]
    public void AdvanceHorizontalSpeed_MatchesSourceDiscreteRunningStep()
    {
        var currentSpeed = 3f * LegacyMovementModel.SourceTicksPerSecond;

        var nextSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            currentSpeed,
            runPower: 1f,
            movementScale: 1f,
            horizontalDirection: 1f,
            state: LegacyMovementState.None,
            isCarryingIntel: false,
            deltaSeconds: 1f / LegacyMovementModel.SourceTicksPerSecond);

        var expected = ((3f + 0.85f) / 1.15f) * LegacyMovementModel.SourceTicksPerSecond;
        Assert.Equal(expected, nextSpeed, 3);
    }

    [Fact]
    public void AdvanceHorizontalSpeed_UsesIntelSpeedCapFromSource()
    {
        var nextSpeed = LegacyMovementModel.AdvanceHorizontalSpeed(
            currentSpeed: 0f,
            runPower: 1f,
            movementScale: 1f,
            horizontalDirection: 1f,
            state: LegacyMovementState.None,
            isCarryingIntel: true,
            deltaSeconds: 1f / LegacyMovementModel.SourceTicksPerSecond);

        var expected = (0.75f / 1.15f) * LegacyMovementModel.SourceTicksPerSecond;
        Assert.Equal(expected, nextSpeed, 3);
    }

    [Fact]
    public void AdvanceVerticalSpeed_MatchesSourceBlastRiseAndClearsStateAtApex()
    {
        var movementState = LegacyMovementState.RocketJuggle;
        var risingSpeed = LegacyMovementModel.AdvanceVerticalSpeed(
            currentSpeed: -2f * LegacyMovementModel.SourceTicksPerSecond,
            isAirborne: true,
            deltaSeconds: 1f / LegacyMovementModel.SourceTicksPerSecond,
            state: ref movementState);

        Assert.Equal(LegacyMovementState.RocketJuggle, movementState);
        Assert.Equal(-1.5f * LegacyMovementModel.SourceTicksPerSecond, risingSpeed, 3);

        var apexState = LegacyMovementState.RocketJuggle;
        var fallingSpeed = LegacyMovementModel.AdvanceVerticalSpeed(
            currentSpeed: 0f,
            isAirborne: true,
            deltaSeconds: 1f / LegacyMovementModel.SourceTicksPerSecond,
            state: ref apexState);

        Assert.Equal(LegacyMovementState.None, apexState);
        Assert.Equal(0.6f * LegacyMovementModel.SourceTicksPerSecond, fallingSpeed, 3);
    }
}
