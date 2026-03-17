namespace OpenGarrison.Core;

public sealed class MineProjectileEntity : SimulationEntity
{
    public const float BlastRadius = 60f;
    public const float BaseExplosionDamage = 25f;
    public const float MaxExplosionDamage = 50f;
    public const float GravityPerTick = 0.2f;
    public const float MaxFallSpeed = 8f;
    public const float BlastImpulse = 10f;

    public MineProjectileEntity(
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
    }

    public PlayerTeam Team { get; }

    public int OwnerId { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public bool IsStickied { get; private set; }

    public bool IsDestroyed { get; private set; }

    public float ExplosionDamage { get; private set; } = BaseExplosionDamage;

    public void AdvanceOneTick()
    {
        PreviousX = X;
        PreviousY = Y;
        ExplosionDamage = float.Min(MaxExplosionDamage, ExplosionDamage + 1f);
        if (IsStickied)
        {
            return;
        }

        VelocityY = float.Min(MaxFallSpeed, VelocityY + GravityPerTick);
        X += VelocityX;
        Y += VelocityY;
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Stick()
    {
        IsStickied = true;
        VelocityX = 0f;
        VelocityY = 0f;
    }

    public void Unstick()
    {
        IsStickied = false;
    }

    public void ApplyImpulse(float velocityX, float velocityY)
    {
        VelocityX += velocityX;
        VelocityY += velocityY;
    }

    public void Destroy()
    {
        IsDestroyed = true;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float velocityX,
        float velocityY,
        bool isStickied,
        bool isDestroyed,
        float explosionDamage)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        IsStickied = isStickied;
        IsDestroyed = isDestroyed;
        ExplosionDamage = explosionDamage;
    }
}

