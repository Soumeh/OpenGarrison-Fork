using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class CustomMapPngImporterTests
{
    [Fact]
    public void Import_MapsLegacyCustomMapAliasesToRoomObjects()
    {
        var mapPath = CreateCustomMapPng(
            """
            {WALKMASK}
            1
            1
            !
            {END WALKMASK}
            {ENTITIES}
            redspawn
            10
            20
            bluespawn
            30
            40
            medCabinet
            50
            60
            redteamgate
            70
            80
            blueteamgate2
            90
            100
            redintelgate
            110
            120
            blueintelgate2
            130
            140
            intelgatehorizontal
            150
            160
            intelgatevertical
            170
            180
            playerwall_horizontal
            190
            200
            bulletwall_horizontal
            210
            220
            CapturePoint
            230
            240
            SetupGate
            250
            260
            ArenaControlPoint
            270
            280
            GeneratorRed
            290
            300
            GeneratorBlue
            310
            320
            teleport_1_point_1
            330
            340
            {END ENTITIES}
            """);

        var imported = CustomMapPngImporter.Import(mapPath);
        Assert.NotNull(imported);
        var room = imported!.Room;

        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "medCabinet" && marker.Type == RoomObjectType.HealingCabinet);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "redteamgate" && marker.Type == RoomObjectType.TeamGate && marker.Team == PlayerTeam.Red);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "blueteamgate2" && marker.Type == RoomObjectType.TeamGate && marker.Team == PlayerTeam.Blue && marker.Width == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "redintelgate" && marker.Type == RoomObjectType.IntelGate && marker.Team == PlayerTeam.Red);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "blueintelgate2" && marker.Type == RoomObjectType.IntelGate && marker.Team == PlayerTeam.Blue && marker.Width == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "intelgatehorizontal" && marker.Type == RoomObjectType.IntelGate && marker.Team is null && marker.Width == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "intelgatevertical" && marker.Type == RoomObjectType.IntelGate && marker.Team is null && marker.Height == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "playerwall_horizontal" && marker.Type == RoomObjectType.PlayerWall && marker.Width == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "bulletwall_horizontal" && marker.Type == RoomObjectType.BulletWall && marker.Width == 60f);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "CapturePoint" && marker.Type == RoomObjectType.CaptureZone);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "SetupGate" && marker.Type == RoomObjectType.ControlPointSetupGate);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "ArenaControlPoint" && marker.Type == RoomObjectType.ArenaControlPoint);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "GeneratorRed" && marker.Type == RoomObjectType.Generator && marker.Team == PlayerTeam.Red);
        Assert.Contains(room.RoomObjects, marker => marker.SourceName == "GeneratorBlue" && marker.Type == RoomObjectType.Generator && marker.Team == PlayerTeam.Blue);
        Assert.Equal(["teleport_1_point_1"], room.UnsupportedEntities);
    }

    [Fact]
    public void Import_PreservesDirectionalAreaMarkersAndBuildsBoundariesFromNextMarkers()
    {
        var mapPath = CreateCustomMapPng(
            """
            {WALKMASK}
            1
            1
            !
            {END WALKMASK}
            {ENTITIES}
            redspawn
            10
            20
            bluespawn
            30
            40
            NextAreaO
            0
            300
            PreviousAreaO
            0
            100
            PreviousAreaO
            0
            300
            NextAreaO
            0
            700
            {END ENTITIES}
            """);

        var imported = CustomMapPngImporter.Import(mapPath);
        Assert.NotNull(imported);

        Assert.Equal([300f, 700f], imported!.Room.AreaBoundaries.ToArray());
        Assert.Collection(
            imported.Room.AreaTransitionMarkers,
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Next, marker.Direction);
                Assert.Equal(300f, marker.Y);
                Assert.Equal("NextAreaO", marker.SourceName);
            },
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Previous, marker.Direction);
                Assert.Equal(100f, marker.Y);
                Assert.Equal("PreviousAreaO", marker.SourceName);
            },
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Previous, marker.Direction);
                Assert.Equal(300f, marker.Y);
            },
            marker =>
            {
                Assert.Equal(AreaTransitionDirection.Next, marker.Direction);
                Assert.Equal(700f, marker.Y);
            });
    }

    private static string CreateCustomMapPng(string levelData)
    {
        var directory = Path.Combine(Path.GetTempPath(), "OpenGarrison-core-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "legacy-map.png");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(writer, "IHDR", BuildIhdrChunk());
        WriteChunk(writer, "tEXt", BuildTextChunk(levelData));
        WriteChunk(writer, "IDAT", BuildIdatChunk());
        WriteChunk(writer, "IEND", []);
        writer.Flush();

        return path;
    }

    private static byte[] BuildIhdrChunk()
    {
        var data = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4, 4), 1);
        data[8] = 8;
        data[9] = 6;
        data[10] = 0;
        data[11] = 0;
        data[12] = 0;
        return data;
    }

    private static byte[] BuildTextChunk(string levelData)
    {
        return [.. Encoding.ASCII.GetBytes("Comment"), 0, .. Encoding.UTF8.GetBytes(levelData)];
    }

    private static byte[] BuildIdatChunk()
    {
        using var memory = new MemoryStream();
        using (var zlibStream = new ZLibStream(memory, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlibStream.WriteByte(0);
            zlibStream.Write(new byte[] { 0, 0, 0, 0 });
        }

        return memory.ToArray();
    }

    private static void WriteChunk(BinaryWriter writer, string chunkType, byte[] data)
    {
        Span<byte> lengthBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, (uint)data.Length);
        writer.Write(lengthBuffer);

        var chunkTypeBytes = Encoding.ASCII.GetBytes(chunkType);
        writer.Write(chunkTypeBytes);
        writer.Write(data);

        var crcInput = new byte[chunkTypeBytes.Length + data.Length];
        chunkTypeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, chunkTypeBytes.Length);
        Span<byte> crcBuffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuffer, ComputeCrc32(crcInput));
        writer.Write(crcBuffer);
    }

    private static uint ComputeCrc32(byte[] data)
    {
        const uint polynomial = 0xedb88320u;
        uint crc = 0xffffffffu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit += 1)
            {
                crc = (crc & 1u) != 0
                    ? (crc >> 1) ^ polynomial
                    : crc >> 1;
            }
        }

        return crc ^ 0xffffffffu;
    }
}
