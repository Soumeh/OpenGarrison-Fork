namespace OpenGarrison.Core;

public sealed record KillFeedEntry(
    string KillerName,
    PlayerTeam KillerTeam,
    string WeaponSpriteName,
    string VictimName,
    PlayerTeam VictimTeam,
    string MessageText = "");
