using System;
using System.Net;
using Xunit;

namespace OpenGarrison.Server.Tests;

public sealed class EndpointRateLimiterTests
{
    [Fact]
    public void TryConsume_LimitsAttemptsAcrossPortsForSameAddress()
    {
        var now = TimeSpan.Zero;
        var limiter = new EndpointRateLimiter(
            maxAttempts: 2,
            window: TimeSpan.FromSeconds(10),
            cooldown: TimeSpan.FromSeconds(5),
            nowProvider: () => now);

        Assert.True(limiter.TryConsume(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4000), out _));
        Assert.True(limiter.TryConsume(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4001), out _));
        Assert.False(limiter.TryConsume(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4002), out var retryAfter));
        Assert.True(retryAfter >= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reset_ClearsCooldownForEndpointAddress()
    {
        var now = TimeSpan.Zero;
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8190);
        var limiter = new EndpointRateLimiter(
            maxAttempts: 1,
            window: TimeSpan.FromSeconds(10),
            cooldown: TimeSpan.FromSeconds(5),
            nowProvider: () => now);

        Assert.True(limiter.TryConsume(endPoint, out _));
        Assert.False(limiter.TryConsume(new IPEndPoint(endPoint.Address, 8191), out _));

        limiter.Reset(endPoint);

        Assert.True(limiter.TryConsume(new IPEndPoint(endPoint.Address, 8192), out _));
    }

    [Fact]
    public void CooldownExpiresAfterConfiguredInterval()
    {
        var now = TimeSpan.Zero;
        var endPoint = new IPEndPoint(IPAddress.Parse("10.0.0.4"), 9000);
        var limiter = new EndpointRateLimiter(
            maxAttempts: 1,
            window: TimeSpan.FromSeconds(10),
            cooldown: TimeSpan.FromSeconds(3),
            nowProvider: () => now);

        Assert.True(limiter.TryConsume(endPoint, out _));
        Assert.False(limiter.TryConsume(endPoint, out _));

        now = TimeSpan.FromSeconds(4);

        Assert.False(limiter.IsLimited(endPoint, out _));
        Assert.True(limiter.TryConsume(endPoint, out _));
    }
}
