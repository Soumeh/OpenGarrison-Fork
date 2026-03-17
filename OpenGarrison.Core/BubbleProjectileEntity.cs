namespace OpenGarrison.Core;

public sealed class BubbleProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 390;
    public const float DamagePerTouch = 0.35f;
    public const float MaxDistanceFromOwner = 160f;
    public const float Radius = 6f;

    public BubbleProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = LifetimeTicks;
    }

    public PlayerTeam Team { get; }

    public int OwnerId { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int TicksRemaining { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick(float ownerX, float ownerY, float ownerVelocityX, float ownerVelocityY, float aimDirectionDegrees)
    {
        PreviousX = X;
        PreviousY = Y;

        var aimRadians = aimDirectionDegrees * (MathF.PI / 180f);
        VelocityX += MathF.Cos(aimRadians) * 0.3f;
        VelocityY += MathF.Sin(aimRadians) * 0.3f;
        VelocityX += ownerVelocityX * 0.015f;
        VelocityY += ownerVelocityY * 0.015f;

        var deltaX = ownerX - X;
        var deltaY = ownerY - Y;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance > 0.001f)
        {
            var pull = distance < 90f ? -1.5f : 1.2f;
            VelocityX += (deltaX / distance) * pull;
            VelocityY += (deltaY / distance) * pull;
        }

        VelocityX *= 0.6f;
        VelocityY *= 0.6f;
        X += VelocityX;
        Y += VelocityY;
        TicksRemaining -= 1;
    }

    public void Bounce()
    {
        VelocityX *= -0.8f;
        VelocityY *= -0.8f;
        TicksRemaining -= 8;
    }

    public void OnCharacterHit()
    {
        TicksRemaining -= 70;
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

    public void ApplyNetworkState(float x, float y, float velocityX, float velocityY, int ticksRemaining)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
    }
}
