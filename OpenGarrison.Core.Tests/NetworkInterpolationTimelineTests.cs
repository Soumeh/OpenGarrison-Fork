using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class NetworkInterpolationTimelineTests
{
    [Fact]
    public void AdvanceTowards_SnapsWhenTimelineFallsTooFarBehind()
    {
        var advanced = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 1.0d,
            targetSeconds: 1.4d,
            deltaSeconds: 1d / 60d);

        Assert.Equal(1.4d, advanced, 6);
    }

    [Fact]
    public void AdvanceTowards_DampsBurstCorrectionsInsteadOfJumpingToTarget()
    {
        var currentSeconds = 1.0d;
        var deltaSeconds = 1d / 60d;
        var targetSeconds = 1.1d;

        var advanced = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds,
            targetSeconds,
            deltaSeconds);

        Assert.True(advanced > currentSeconds + deltaSeconds);
        Assert.True(advanced < targetSeconds);
    }

    [Fact]
    public void AdvanceTowards_ClampsSmallOvershootWhenSnapshotsPause()
    {
        var advanced = NetworkInterpolationTimeline.AdvanceTowards(
            currentSeconds: 1.0d,
            targetSeconds: 1.0d,
            deltaSeconds: 0.05d);

        Assert.True(advanced <= 1.04d + 1e-6d);
    }
}
