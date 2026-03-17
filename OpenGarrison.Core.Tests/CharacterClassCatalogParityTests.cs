using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class CharacterClassCatalogParityTests
{
    private static readonly IReadOnlyDictionary<PlayerClass, (string SourceName, string WeaponName)> ClassSources =
        new Dictionary<PlayerClass, (string SourceName, string WeaponName)>
        {
            [PlayerClass.Scout] = ("Scout", "Scattergun"),
            [PlayerClass.Engineer] = ("Engineer", "Shotgun"),
            [PlayerClass.Pyro] = ("Pyro", "Flamethrower"),
            [PlayerClass.Soldier] = ("Soldier", "Rocketlauncher"),
            [PlayerClass.Demoman] = ("Demoman", "Minegun"),
            [PlayerClass.Heavy] = ("Heavy", "Minigun"),
            [PlayerClass.Sniper] = ("Sniper", "Rifle"),
            [PlayerClass.Medic] = ("Medic", "Medigun"),
            [PlayerClass.Spy] = ("Spy", "Revolver"),
            [PlayerClass.Quote] = ("Quote", "Blade"),
        };

    [Fact]
    public void Definitions_AreDerivedFromSourceCreateEvents()
    {
        foreach (var (playerClass, sourceInfo) in ClassSources)
        {
            var source = ReadSourceValues(sourceInfo.SourceName);
            var definition = CharacterClassCatalog.GetDefinition(playerClass);

            Assert.Equal(source.MaxHealth, definition.MaxHealth);
            Assert.Equal(source.CanDoubleJump, definition.MaxAirJumps == 1);
            Assert.Equal(source.TauntLengthFrames, definition.TauntLengthFrames);
            Assert.Equal(sourceInfo.WeaponName, source.WeaponName);
            Assert.Equal(GetExpectedDisplayName(sourceInfo.WeaponName), definition.PrimaryWeapon.DisplayName);
            Assert.Equal(source.RunPower, definition.RunPower, 3);
            Assert.Equal(source.JumpStrength, definition.JumpStrength, 3);
            Assert.Equal(LegacyMovementModel.GetMaxRunSpeed(source.RunPower), definition.MaxRunSpeed, 3);
            Assert.Equal(LegacyMovementModel.GetContinuousRunDrive(source.RunPower), definition.GroundAcceleration, 3);
            Assert.Equal(LegacyMovementModel.GetContinuousRunDrive(source.RunPower), definition.GroundDeceleration, 3);
            Assert.Equal(LegacyMovementModel.GetGravityPerSecondSquared(), definition.Gravity, 3);
            Assert.Equal(LegacyMovementModel.GetJumpSpeed(source.JumpStrength), definition.JumpSpeed, 3);
        }
    }

    private static SourceClassValues ReadSourceValues(string sourceName)
    {
        var path = ProjectSourceLocator.FindFile($"Source/OpenGarrison/Objects/Characters/{sourceName}.events/Create.xml");
        Assert.False(string.IsNullOrWhiteSpace(path));

        var document = XDocument.Load(path!);
        var code = document.Root?
            .Descendants("argument")
            .Select(element => element.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        Assert.False(string.IsNullOrWhiteSpace(code));

        return new SourceClassValues(
            CanDoubleJump: ReadIntAssignment(code!, "canDoublejump") != 0,
            JumpStrength: ReadFloatAssignment(code!, "jumpStrength"),
            RunPower: ReadFloatAssignment(code!, "runPower"),
            MaxHealth: ReadIntAssignment(code!, "maxHp"),
            WeaponName: ReadStringAssignment(code!, @"weapons\[0\]"),
            TauntLengthFrames: ReadIntAssignment(code!, "tauntlength"));
    }

    private static int ReadIntAssignment(string code, string variableName)
    {
        var value = ReadStringAssignment(code, variableName);
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static float ReadFloatAssignment(string code, string variableName)
    {
        var value = ReadStringAssignment(code, variableName);
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string ReadStringAssignment(string code, string variablePattern)
    {
        var match = Regex.Match(
            code,
            $@"\b{variablePattern}\s*=\s*([A-Za-z0-9._]+)",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Expected assignment for {variablePattern}.");
        return match.Groups[1].Value;
    }

    private static string GetExpectedDisplayName(string sourceWeaponName)
    {
        return sourceWeaponName switch
        {
            "Rocketlauncher" => "Rocket Launcher",
            "Minegun" => "Mine Launcher",
            _ => sourceWeaponName,
        };
    }

    private readonly record struct SourceClassValues(
        bool CanDoubleJump,
        float JumpStrength,
        float RunPower,
        int MaxHealth,
        string WeaponName,
        int TauntLengthFrames);
}
