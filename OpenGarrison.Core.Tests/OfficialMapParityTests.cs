using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class OfficialMapParityTests
{
    private static readonly IReadOnlyDictionary<string, OfficialMapExpectation> OfficialMaps =
        new Dictionary<string, OfficialMapExpectation>(StringComparer.OrdinalIgnoreCase)
        {
            ["ClassicWell"] = new(GameModeKind.CaptureTheFlag, 1, "zclassicwellB"),
            ["Conflict"] = new(GameModeKind.CaptureTheFlag, 1, "ConflictB"),
            ["Destroy"] = new(GameModeKind.Generator, 1, "DestroyS"),
            ["Dirtbowl"] = new(GameModeKind.ControlPoint, 3, "DirtbowlB"),
            ["Egypt"] = new(GameModeKind.ControlPoint, 1, "EgyptB"),
            ["Lumberyard"] = new(GameModeKind.Arena, 1, "LumberyardB"),
            ["Montane"] = new(GameModeKind.Arena, 1, "MontaneB"),
            ["Orange"] = new(GameModeKind.CaptureTheFlag, 1, "OrangeB"),
            ["Truefort"] = new(GameModeKind.CaptureTheFlag, 1, "TruefortB"),
            ["TwodFortTwo"] = new(GameModeKind.CaptureTheFlag, 1, "TwodFortTwoB"),
            ["Waterway"] = new(GameModeKind.CaptureTheFlag, 1, "WaterwayB"),
        };

    [Fact]
    public void OfficialCatalog_ContainsExpectedMapsModesAndCollisionMasks()
    {
        var catalog = SimpleLevelFactory.GetAvailableSourceLevels();
        var assets = GameMakerAssetManifestImporter.ImportProjectAssets();
        var expectedNames = OfficialMaps.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actualNames = catalog
            .Select(entry => entry.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedNames, actualNames);

        foreach (var entry in catalog)
        {
            var expectation = OfficialMaps[entry.Name];
            Assert.Equal(expectation.Mode, entry.Mode);
            Assert.False(string.IsNullOrWhiteSpace(entry.CollisionMaskSourcePath));
            Assert.True(File.Exists(entry.CollisionMaskSourcePath));
            Assert.Contains(expectation.BackgroundAssetName, assets.Backgrounds.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OfficialMaps_LoadFromSourceWithExpectedObjectiveMarkers()
    {
        foreach (var (mapName, expectation) in OfficialMaps)
        {
            var level = SimpleLevelFactory.CreateImportedLevel(mapName);

            Assert.NotNull(level);
            Assert.True(level!.ImportedFromSource);
            Assert.Equal(expectation.Mode, level.Mode);
            Assert.Equal(expectation.AreaCount, level.MapAreaCount);
            Assert.Equal(expectation.BackgroundAssetName, level.BackgroundAssetName);
            Assert.NotEmpty(level.RedSpawns);
            Assert.NotEmpty(level.BlueSpawns);
            Assert.NotEmpty(level.Solids);

            switch (expectation.Mode)
            {
                case GameModeKind.CaptureTheFlag:
                    Assert.Equal(2, level.IntelBases.Count);
                    break;
                case GameModeKind.Arena:
                    Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.ArenaControlPoint));
                    Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.CaptureZone));
                    break;
                case GameModeKind.ControlPoint:
                    Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.ControlPoint));
                    Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.CaptureZone));
                    break;
                case GameModeKind.Generator:
                    Assert.Equal(2, level.GetRoomObjects(RoomObjectType.Generator).Count);
                    break;
            }
        }
    }

    [Fact]
    public void Dirtbowl_AllAreasLoadWithExpectedAreaMetadata()
    {
        for (var areaIndex = 1; areaIndex <= 3; areaIndex += 1)
        {
            var level = SimpleLevelFactory.CreateImportedLevel("Dirtbowl", areaIndex);

            Assert.NotNull(level);
            Assert.Equal(areaIndex, level!.MapAreaIndex);
            Assert.Equal(3, level.MapAreaCount);
            Assert.NotEmpty(level.RedSpawns);
            Assert.NotEmpty(level.BlueSpawns);
            Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.ControlPoint));
            Assert.NotEmpty(level.GetRoomObjects(RoomObjectType.CaptureZone));
        }
    }

    [Fact]
    public void OfficialMapRoomObjects_AreSupportedOrExplicitlyIgnored()
    {
        var supportedObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ArenaControlPoint",
            "BlueTeamGate",
            "BlueTeamGate2",
            "BulletWall",
            "CaptureZone",
            "ControlPoint1",
            "ControlPoint2",
            "ControlPoint3",
            "ControlPoint4",
            "ControlPoint5",
            "ControlPointSetupGate",
            "GeneratorBlue",
            "GeneratorRed",
            "HealingCabinet",
            "IntelligenceBaseBlue",
            "IntelligenceBaseRed",
            "IntelligenceBlue",
            "IntelligenceRed",
            "NextAreaO",
            "PlayerWallHorizontal",
            "RedTeamGate",
            "RedTeamGate2",
            "SpawnPointBlue",
            "SpawnPointRed",
            "SpawnRoom",
        };
        var explicitlyIgnored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Desensitizer",
        };
        var unknownObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ignoredObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapFile in GetOfficialMapFiles())
        {
            var document = XDocument.Load(mapFile);
            var objects = document.Root?
                .Element("instances")?
                .Elements("instance")
                .Select(instance => (string?)instance.Element("object"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];

            foreach (var objectName in objects)
            {
                if (supportedObjects.Contains(objectName))
                {
                    continue;
                }

                if (explicitlyIgnored.Contains(objectName))
                {
                    ignoredObjects.Add(objectName);
                    continue;
                }

                unknownObjects.Add(objectName);
            }
        }

        Assert.Empty(unknownObjects);
        Assert.Equal(["Desensitizer"], ignoredObjects.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [Fact]
    public void SampleRotation_CoversAllOfficialPlayableMaps()
    {
        var sampleRotationPath = ProjectSourceLocator.FindFile("sampleMapRotation.txt");

        Assert.NotNull(sampleRotationPath);

        var entries = File.ReadAllLines(sampleRotationPath!)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expectedEntry in new[]
        {
            "ctf_truefort",
            "ctf_2dfort",
            "ctf_conflict",
            "ctf_classicwell",
            "ctf_waterway",
            "ctf_orange",
            "cp_egypt",
            "cp_dirtbowl",
            "arena_montane",
            "arena_lumberyard",
            "gen_destroy",
        })
        {
            Assert.Contains(expectedEntry, entries);
        }
    }

    private static string[] GetOfficialMapFiles()
    {
        var mapsDirectory = ProjectSourceLocator.FindDirectory(Path.Combine("OpenGarrison.Core", "Content", "Rooms", "Maps"));

        Assert.NotNull(mapsDirectory);

        return Directory.EnumerateFiles(mapsDirectory!, "*.xml")
            .Where(path =>
            {
                var mapName = Path.GetFileNameWithoutExtension(path);
                return !string.Equals(mapName, "_resources.list", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(mapName, "CustomMapRoom", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record OfficialMapExpectation(GameModeKind Mode, int AreaCount, string BackgroundAssetName);
}
