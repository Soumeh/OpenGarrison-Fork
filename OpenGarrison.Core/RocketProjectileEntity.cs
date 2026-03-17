namespace OpenGarrison.Core;

public sealed class RocketProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 200;
    public const int DirectHitDamage = 25;
    public const float ExplosionDamage = 30f;
    public const float BlastRadius = 65f;
    public const float Knockback = 8f;

    public RocketProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float speed,
        float directionRadians) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = LifetimeTicks;
    }

    public PlayerTeam Team { get; private set; }

    public int OwnerId { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float DirectionRadians { get; private set; }

    public float Speed { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick()
    {
        PreviousX = X;
        PreviousY = Y;
        X += MathF.Cos(DirectionRadians) * Speed;
        Y += MathF.Sin(DirectionRadians) * Speed;
        Speed += 1f;
        Speed *= 0.92f;
        TicksRemaining -= 1;
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Destroy()
    {
        TicksRemaining = 0;
    }

    public void Reflect(int ownerId, PlayerTeam team, float directionRadians, float speed)
    {
        OwnerId = ownerId;
        Team = team;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = LifetimeTicks;
        PreviousX = X;
        PreviousY = Y;
    }

    public void ApplyNetworkState(float x, float y, float directionRadians, float speed, int ticksRemaining)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = ticksRemaining;
    }

    public void ApplyNetworkState(float x, float y, float previousX, float previousY, float directionRadians, float speed, int ticksRemaining)
    {
        PreviousX = previousX;
        PreviousY = previousY;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = ticksRemaining;
    }
}

