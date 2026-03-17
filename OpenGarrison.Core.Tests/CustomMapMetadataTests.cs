using System;
using System.IO;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

[CollectionDefinition("RuntimeMaps", DisableParallelization = true)]
public sealed class RuntimeMapsCollectionDefinition
{
}

[Collection("RuntimeMaps")]
public sealed class CustomMapMetadataTests : IDisposable
{
    private readonly string _levelName = "test_custom_" + Guid.NewGuid().ToString("N");

    [Fact]
    public void ParseHash_AcceptsLegacyMd5AndSha256Formats()
    {
        var md5 = CustomMapHashService.ParseHash("d41d8cd98f00b204e9800998ecf8427e");
        var sha256 = CustomMapHashService.ParseHash("sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        Assert.Equal(CustomMapHashAlgorithm.Md5, md5.Algorithm);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", md5.Value);
        Assert.Equal(CustomMapHashAlgorithm.Sha256, sha256.Algorithm);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", sha256.Value);
    }

    [Fact]
    public void LocatorStore_ReadsLegacySingleLineLocatorFiles()
    {
        var locatorPath = Path.Combine(RuntimePaths.MapsDirectory, _levelName + ".locator");
        File.WriteAllText(locatorPath, "https://example.invalid/map.png");

        var metadata = CustomMapLocatorStore.TryReadMapMetadata(_levelName);

        Assert.True(metadata.HasValue);
        Assert.Equal("https://example.invalid/map.png", metadata.Value.SourceUrl);
        Assert.Equal(string.Empty, metadata.Value.Md5Hash);
        Assert.Equal(string.Empty, metadata.Value.Sha256Hash);
        Assert.Equal("https://example.invalid/map.png", CustomMapLocatorStore.TryReadMapUrl(_levelName));
    }

    [Fact]
    public void DescriptorResolver_UsesMd5ForNetworkHashAndBackfillsLocatorMetadata()
    {
        var mapPath = CustomMapLocatorStore.GetMapPath(_levelName);
        File.WriteAllBytes(mapPath, [1, 2, 3, 4, 5, 6]);
        File.WriteAllText(Path.Combine(RuntimePaths.MapsDirectory, _levelName + ".locator"), "https://example.invalid/source-map.png");

        var resolved = CustomMapDescriptorResolver.TryResolve(_levelName, out var descriptor);
        var metadata = CustomMapLocatorStore.TryReadMapMetadata(_levelName);

        Assert.True(resolved);
        Assert.Equal(CustomMapHashService.ComputeMd5(mapPath), descriptor.ContentHash);
        Assert.Equal(descriptor.ContentHash, descriptor.LegacyMd5Hash);
        Assert.Equal(CustomMapHashService.ComputeSha256(mapPath), descriptor.Sha256Hash);
        Assert.Equal("https://example.invalid/source-map.png", descriptor.SourceUrl);
        Assert.True(metadata.HasValue);
        Assert.Equal(descriptor.LegacyMd5Hash, metadata.Value.Md5Hash);
        Assert.Equal(descriptor.Sha256Hash, metadata.Value.Sha256Hash);
    }

    public void Dispose()
    {
        DeleteIfExists(CustomMapLocatorStore.GetMapPath(_levelName));
        DeleteIfExists(Path.Combine(RuntimePaths.MapsDirectory, _levelName + ".locator"));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
