namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    internal readonly record struct PredictionState(
        PlayerTeam Team,
        CharacterClassDefinition ClassDefinition,
        bool IsAlive,
        float X,
        float Y,
        float HorizontalSpeed,
        float VerticalSpeed,
        float LegacyStateTickAccumulator,
        LegacyMovementState MovementState,
        bool IsGrounded,
        int Health,
        float Metal,
        bool IsCarryingIntel,
        int IntelPickupCooldownTicks,
        bool IsInSpawnRoom,
        int RemainingAirJumps,
        float FacingDirectionX,
        float AimDirectionDegrees,
        int CurrentShells,
        int PrimaryCooldownTicks,
        int ReloadTicksUntilNextShell,
        float ContinuousDamageAccumulator,
        bool IsHeavyEating,
        int HeavyEatTicksRemaining,
        float HeavyHealingAccumulator,
        bool IsTaunting,
        float TauntFrameIndex,
        bool IsSniperScoped,
        int SniperChargeTicks,
        int UberTicksRemaining,
        int? MedicHealTargetId,
        bool IsMedicHealing,
        float MedicUberCharge,
        bool IsMedicUberReady,
        bool IsMedicUbering,
        int MedicNeedleCooldownTicks,
        int MedicNeedleRefillTicks,
        float ContinuousHealingAccumulator,
        int QuoteBubbleCount,
        int QuoteBladesOut,
        int PyroAirblastCooldownTicks,
        bool IsSpyCloaked,
        float SpyCloakAlpha,
        int SpyBackstabWindupTicksRemaining,
        int SpyBackstabRecoveryTicksRemaining,
        int SpyBackstabVisualTicksRemaining,
        float SpyBackstabDirectionDegrees,
        bool SpyBackstabHitboxPending,
        bool IsSpyVisibleToEnemies,
        int Kills,
        int Deaths,
        int Caps,
        int HealPoints,
        bool IsChatBubbleVisible,
        int ChatBubbleFrameIndex,
        float ChatBubbleAlpha,
        bool IsChatBubbleFading,
        int ChatBubbleTicksRemaining);

    internal PredictionState CapturePredictionState()
    {
        return new PredictionState(
            Team,
            ClassDefinition,
            IsAlive,
            X,
            Y,
            HorizontalSpeed,
            VerticalSpeed,
            LegacyStateTickAccumulator,
            MovementState,
            IsGrounded,
            Health,
            Metal,
            IsCarryingIntel,
            IntelPickupCooldownTicks,
            IsInSpawnRoom,
            RemainingAirJumps,
            FacingDirectionX,
            AimDirectionDegrees,
            CurrentShells,
            PrimaryCooldownTicks,
            ReloadTicksUntilNextShell,
            ContinuousDamageAccumulator,
            IsHeavyEating,
            HeavyEatTicksRemaining,
            HeavyHealingAccumulator,
            IsTaunting,
            TauntFrameIndex,
            IsSniperScoped,
            SniperChargeTicks,
            UberTicksRemaining,
            MedicHealTargetId,
            IsMedicHealing,
            MedicUberCharge,
            IsMedicUberReady,
            IsMedicUbering,
            MedicNeedleCooldownTicks,
            MedicNeedleRefillTicks,
            ContinuousHealingAccumulator,
            QuoteBubbleCount,
            QuoteBladesOut,
            PyroAirblastCooldownTicks,
            IsSpyCloaked,
            SpyCloakAlpha,
            SpyBackstabWindupTicksRemaining,
            SpyBackstabRecoveryTicksRemaining,
            SpyBackstabVisualTicksRemaining,
            SpyBackstabDirectionDegrees,
            SpyBackstabHitboxPending,
            IsSpyVisibleToEnemies,
            Kills,
            Deaths,
            Caps,
            HealPoints,
            IsChatBubbleVisible,
            ChatBubbleFrameIndex,
            ChatBubbleAlpha,
            IsChatBubbleFading,
            ChatBubbleTicksRemaining);
    }

    internal void RestorePredictionState(in PredictionState state)
    {
        Team = state.Team;
        ClassDefinition = state.ClassDefinition;
        IsAlive = state.IsAlive;
        X = state.X;
        Y = state.Y;
        HorizontalSpeed = state.HorizontalSpeed;
        VerticalSpeed = state.VerticalSpeed;
        LegacyStateTickAccumulator = state.LegacyStateTickAccumulator;
        MovementState = state.MovementState;
        IsGrounded = state.IsGrounded;
        Health = state.Health;
        Metal = state.Metal;
        IsCarryingIntel = state.IsCarryingIntel;
        IntelPickupCooldownTicks = state.IntelPickupCooldownTicks;
        IsInSpawnRoom = state.IsInSpawnRoom;
        RemainingAirJumps = state.RemainingAirJumps;
        FacingDirectionX = state.FacingDirectionX;
        AimDirectionDegrees = state.AimDirectionDegrees;
        CurrentShells = state.CurrentShells;
        PrimaryCooldownTicks = state.PrimaryCooldownTicks;
        ReloadTicksUntilNextShell = state.ReloadTicksUntilNextShell;
        ContinuousDamageAccumulator = state.ContinuousDamageAccumulator;
        IsHeavyEating = state.IsHeavyEating;
        HeavyEatTicksRemaining = state.HeavyEatTicksRemaining;
        HeavyHealingAccumulator = state.HeavyHealingAccumulator;
        IsTaunting = state.IsTaunting;
        TauntFrameIndex = state.TauntFrameIndex;
        IsSniperScoped = state.IsSniperScoped;
        SniperChargeTicks = state.SniperChargeTicks;
        UberTicksRemaining = state.UberTicksRemaining;
        MedicHealTargetId = state.MedicHealTargetId;
        IsMedicHealing = state.IsMedicHealing;
        MedicUberCharge = state.MedicUberCharge;
        IsMedicUberReady = state.IsMedicUberReady;
        IsMedicUbering = state.IsMedicUbering;
        MedicNeedleCooldownTicks = state.MedicNeedleCooldownTicks;
        MedicNeedleRefillTicks = state.MedicNeedleRefillTicks;
        ContinuousHealingAccumulator = state.ContinuousHealingAccumulator;
        QuoteBubbleCount = state.QuoteBubbleCount;
        QuoteBladesOut = state.QuoteBladesOut;
        PyroAirblastCooldownTicks = state.PyroAirblastCooldownTicks;
        IsSpyCloaked = state.IsSpyCloaked;
        SpyCloakAlpha = float.Clamp(state.SpyCloakAlpha, 0f, 1f);
        SpyBackstabWindupTicksRemaining = state.SpyBackstabWindupTicksRemaining;
        SpyBackstabRecoveryTicksRemaining = state.SpyBackstabRecoveryTicksRemaining;
        SpyBackstabVisualTicksRemaining = state.SpyBackstabVisualTicksRemaining;
        SpyBackstabDirectionDegrees = state.SpyBackstabDirectionDegrees;
        SpyBackstabHitboxPending = state.SpyBackstabHitboxPending;
        IsSpyVisibleToEnemies = state.IsSpyVisibleToEnemies;
        Kills = state.Kills;
        Deaths = state.Deaths;
        Caps = state.Caps;
        HealPoints = state.HealPoints;
        IsChatBubbleVisible = state.IsChatBubbleVisible;
        ChatBubbleFrameIndex = state.ChatBubbleFrameIndex;
        ChatBubbleAlpha = state.ChatBubbleAlpha;
        IsChatBubbleFading = state.IsChatBubbleFading;
        ChatBubbleTicksRemaining = state.ChatBubbleTicksRemaining;
    }

    public void ApplyNetworkState(
        PlayerTeam team,
        CharacterClassDefinition classDefinition,
        bool isAlive,
        float x,
        float y,
        float horizontalSpeed,
        float verticalSpeed,
        int health,
        int currentShells,
        int kills,
        int deaths,
        int caps,
        int healPoints,
        float metal,
        bool isGrounded,
        bool isCarryingIntel,
        bool isSpyCloaked,
        float spyCloakAlpha,
        bool isUbered,
        bool isHeavyEating,
        int heavyEatTicksRemaining,
        bool isSniperScoped,
        int sniperChargeTicks,
        float facingDirectionX,
        float aimDirectionDegrees,
        bool isTaunting,
        float tauntFrameIndex,
        bool isChatBubbleVisible,
        int chatBubbleFrameIndex,
        float chatBubbleAlpha)
    {
        Team = team;
        ClassDefinition = classDefinition;
        X = x;
        Y = y;
        HorizontalSpeed = horizontalSpeed;
        VerticalSpeed = verticalSpeed;
        LegacyStateTickAccumulator = 0f;
        MovementState = LegacyMovementState.None;
        IsGrounded = isGrounded;
        IsAlive = isAlive;
        Health = int.Clamp(health, 0, MaxHealth);
        CurrentShells = int.Clamp(currentShells, 0, MaxShells);
        Kills = Math.Max(0, kills);
        Deaths = Math.Max(0, deaths);
        Caps = Math.Max(0, caps);
        HealPoints = Math.Max(0, healPoints);
        Metal = float.Clamp(metal, 0f, MaxMetal);
        IsCarryingIntel = isCarryingIntel;
        IsSpyCloaked = isSpyCloaked;
        SpyCloakAlpha = float.Clamp(spyCloakAlpha, 0f, 1f);
        SpyBackstabWindupTicksRemaining = 0;
        SpyBackstabRecoveryTicksRemaining = 0;
        SpyBackstabVisualTicksRemaining = 0;
        SpyBackstabDirectionDegrees = 0f;
        SpyBackstabHitboxPending = false;
        IsSpyVisibleToEnemies = IsSpyCloaked && SpyCloakAlpha > 0f;
        UberTicksRemaining = isUbered ? DefaultUberRefreshTicks : 0;
        IsHeavyEating = isHeavyEating;
        HeavyEatTicksRemaining = Math.Max(0, heavyEatTicksRemaining);
        IsSniperScoped = isSniperScoped;
        SniperChargeTicks = Math.Max(0, sniperChargeTicks);
        if (!IsHeavyEating)
        {
            HeavyHealingAccumulator = 0f;
        }
        if (ClassId != PlayerClass.Quote)
        {
            QuoteBubbleCount = 0;
            QuoteBladesOut = 0;
        }
        if (ClassId != PlayerClass.Pyro)
        {
            PyroAirblastCooldownTicks = 0;
        }
        FacingDirectionX = facingDirectionX;
        AimDirectionDegrees = aimDirectionDegrees;
        IsTaunting = isTaunting;
        TauntFrameIndex = tauntFrameIndex;
        IsChatBubbleVisible = isChatBubbleVisible;
        ChatBubbleFrameIndex = chatBubbleFrameIndex;
        ChatBubbleAlpha = chatBubbleAlpha;
        IsChatBubbleFading = false;
        ChatBubbleTicksRemaining = 0;

        if (!IsChatBubbleVisible)
        {
            ChatBubbleFrameIndex = 0;
            ChatBubbleAlpha = 0f;
        }

        if (!IsAlive)
        {
            Health = 0;
            IsCarryingIntel = false;
            IsSniperScoped = false;
            SniperChargeTicks = 0;
        }
    }
}
