using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class AreaTransitionMetadataTests
{
    [Fact]
    public void BuildAreaBoundaries_FallsBackToPreviousMarkersWhenNextMarkersAreMissing()
    {
        AreaTransitionMarker[] markers =
        [
            new AreaTransitionMarker(0f, 450f, AreaTransitionDirection.Previous, "PreviousAreaO"),
            new AreaTransitionMarker(0f, 150f, AreaTransitionDirection.Previous, "PreviousAreaO"),
            new AreaTransitionMarker(0f, 450f, AreaTransitionDirection.Previous, "PreviousAreaO"),
        ];

        var boundaries = AreaTransitionMetadata.BuildAreaBoundaries(markers);

        Assert.Equal([150f, 450f], boundaries);
    }
}
