using System;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class GameMakerRoomMetadataImporterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "OpenGarrison-room-import-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Import_PreservesDirectionalAreaMarkersAndUsesNextMarkersForBoundaries()
    {
        Directory.CreateDirectory(_tempDirectory);
        var roomPath = Path.Combine(_tempDirectory, "test-room.xml");
        File.WriteAllText(roomPath, """
<?xml version="1.0" encoding="UTF-8" standalone="no"?>
<room>
  <size width="800" height="1200" />
  <backgrounds>
    <backgroundDef>
      <backgroundImage>BackgroundTest</backgroundImage>
      <visibleOnRoomStart>true</visibleOnRoomStart>
    </backgroundDef>
  </backgrounds>
  <instances>
    <instance>
      <object>SpawnPointRed</object>
      <position x="10" y="20" />
    </instance>
    <instance>
      <object>SpawnPointBlue</object>
      <position x="30" y="40" />
    </instance>
    <instance>
      <object>NextAreaO</object>
      <position x="0" y="300" />
    </instance>
    <instance>
      <object>PreviousAreaO</object>
      <position x="0" y="100" />
    </instance>
    <instance>
      <object>NextAreaO</object>
      <position x="0" y="700" />
    </instance>
    <instance>
      <object>TotallyUnknownObject</object>
      <position x="0" y="900" />
    </instance>
  </instances>
</room>
""");

        var imported = GameMakerRoomMetadataImporter.Import(roomPath);

        Assert.NotNull(imported);
        Assert.Equal([300f, 700f], imported!.AreaBoundaries);
        Assert.Collection(
            imported.AreaTransitionMarkers,
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Next, marker.Direction);
                Assert.Equal(300f, marker.Y);
            },
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Previous, marker.Direction);
                Assert.Equal(100f, marker.Y);
            },
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Next, marker.Direction);
                Assert.Equal(700f, marker.Y);
            });
        Assert.Equal(["TotallyUnknownObject"], imported.UnsupportedEntities);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
