using System;
using System.IO;
using System.Linq;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class IniConfigurationFileTests : IDisposable
{
    private static readonly string[] ExpectedPreferredRotation = ["Lumberyard", "Truefort"];
    private static readonly string[] ExpectedLegacyRotationPrefix = ["Egypt", "Truefort", "TwodFortTwo"];
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "OpenGarrison-ini-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsSectionsAndTypedValues()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "OpenGarrison.ini");
        var document = new IniConfigurationFile();
        document.SetString("Settings", "PlayerName", "Tester");
        document.SetBool("Settings", "Fullscreen", true);
        document.SetInt("Server", "HostingPort", 8190);

        document.Save(path);

        var loaded = IniConfigurationFile.Load(path);
        Assert.Equal("Tester", loaded.GetString("Settings", "PlayerName"));
        Assert.True(loaded.GetBool("Settings", "Fullscreen"));
        Assert.Equal(8190, loaded.GetInt("Server", "HostingPort"));
    }

    [Fact]
    public void Load_IgnoresCommentsAndUsesFallbacksForInvalidValues()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "comments.ini");
        File.WriteAllText(path, """
; comment
# comment
[Settings]
Fullscreen=notabool
Particles=abc
PlayerName=Demo
""");

        var loaded = IniConfigurationFile.Load(path);
        Assert.Equal("Demo", loaded.GetString("Settings", "PlayerName", "Player"));
        Assert.False(loaded.GetBool("Settings", "Fullscreen", false));
        Assert.Equal(2, loaded.GetInt("Settings", "Particles", 2));
    }

    [Fact]
    public void GetBool_AcceptsNumericValues()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "bools.ini");
        File.WriteAllText(path, """
[Settings]
IngameMusic=1
Kill Cam=0
""");

        var loaded = IniConfigurationFile.Load(path);
        Assert.True(loaded.GetBool("Settings", "IngameMusic"));
        Assert.False(loaded.GetBool("Settings", "Kill Cam", true));
    }

    [Fact]
    public void OpenGarrisonPreferencesDocument_SaveAndLoad_UsesSharedSectionsAndMapsModel()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "OpenGarrison.ini");
        var preferences = new OpenGarrisonPreferencesDocument
        {
            PlayerName = "HostPlayer",
            Fullscreen = true,
            VSync = true,
            IngameMusicEnabled = false,
            HostSettings = new OpenGarrisonHostSettings
            {
                ServerName = "Parity Server",
                Port = 9000,
                Slots = 12,
                Password = "secret",
                TimeLimitMinutes = 20,
                CapLimit = 6,
                RespawnSeconds = 7,
                TickRate = 60,
                LobbyAnnounceEnabled = false,
                AutoBalanceEnabled = false,
                DedicatedModeEnabled = true,
                MapRotationFile = "custom-rotation.txt",
                StockMapRotation = OpenGarrisonStockMapCatalog.CreateDefaultEntries(),
            },
        };
        preferences.HostSettings.StockMapRotation.First(entry => entry.LevelName == "Lumberyard").Order = 1;
        preferences.HostSettings.StockMapRotation.First(entry => entry.LevelName == "Truefort").Order = 2;
        preferences.HostSettings.StockMapRotation.First(entry => entry.LevelName == "Destroy").Order = 0;

        preferences.Save(path);

        var raw = IniConfigurationFile.Load(path);
        Assert.True(raw.ContainsKey("Settings", "UseLobby"));
        Assert.True(raw.ContainsKey("Settings", "HostingPort"));
        Assert.True(raw.ContainsKey("Settings", "PlayerLimit"));
        Assert.True(raw.ContainsKey("Server", "MapRotation"));
        Assert.True(raw.ContainsKey("Server.Advanced", "TickRate"));
        Assert.True(raw.ContainsKey("Maps", "arena_lumberyard"));

        var loaded = OpenGarrisonPreferencesDocument.Load(path);
        Assert.Equal("HostPlayer", loaded.PlayerName);
        Assert.Equal("Parity Server", loaded.HostSettings.ServerName);
        Assert.Equal(9000, loaded.HostSettings.Port);
        Assert.Equal(12, loaded.HostSettings.Slots);
        Assert.Equal(60, loaded.HostSettings.TickRate);
        Assert.False(loaded.HostSettings.LobbyAnnounceEnabled);
        Assert.True(loaded.HostSettings.DedicatedModeEnabled);
        Assert.Equal("custom-rotation.txt", loaded.HostSettings.MapRotationFile);
        Assert.Equal(
            ExpectedPreferredRotation,
            OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(loaded.HostSettings.StockMapRotation).Take(2).ToArray());
        Assert.DoesNotContain(
            "Destroy",
            OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(loaded.HostSettings.StockMapRotation));
    }

    [Fact]
    public void OpenGarrisonPreferencesDocument_Load_PromotesLegacySelectedMapWhenMapsSectionIsMissing()
    {
        Directory.CreateDirectory(_tempDirectory);
        var path = Path.Combine(_tempDirectory, "legacy-selected-map.ini");
        File.WriteAllText(path, """
        [Settings]
        UseLobby=1
        HostingPort=8190
        PlayerLimit=10
        [Server]
        SelectedMap=cp_egypt
        """);

        var loaded = OpenGarrisonPreferencesDocument.Load(path);

        Assert.Equal("Egypt", loaded.HostSettings.GetFirstIncludedMapLevelName());
        Assert.Equal(
            ExpectedLegacyRotationPrefix,
            OpenGarrisonStockMapCatalog.GetOrderedIncludedMapLevelNames(loaded.HostSettings.StockMapRotation).Take(3).ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
