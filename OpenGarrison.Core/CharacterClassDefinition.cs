namespace OpenGarrison.Core;

public sealed record CharacterClassDefinition(
    PlayerClass Id,
    string DisplayName,
    PrimaryWeaponDefinition PrimaryWeapon,
    int MaxHealth,
    float Width,
    float Height,
    float RunPower,
    float JumpStrength,
    float MaxRunSpeed,
    float GroundAcceleration,
    float GroundDeceleration,
    float Gravity,
    float JumpSpeed,
    int MaxAirJumps,
    int TauntLengthFrames);
