using DrawSound.Core.Audio;

namespace DrawSound.Tests;

public class BezierWaveSamplerTests
{
    private const float Tolerance = 0.01f;

    [Fact]
    public void SampleToWaveTable_EmptyNodes_ReturnsSilence()
    {
        // Arrange
        var nodes = new List<BezierNode>();

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 100);

        // Assert
        Assert.Equal(100, result.Length);
        Assert.All(result, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void SampleToWaveTable_SingleNode_ReturnsFlatLine()
    {
        // Arrange
        var nodes = new List<BezierNode> { new(0.5f, 0.3f) };

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 100);

        // Assert
        Assert.Equal(100, result.Length);
        Assert.All(result, sample => Assert.Equal(0.3f, sample, 4));
    }

    [Fact]
    public void SampleToWaveTable_TwoNodes_InterpolatesBetween()
    {
        // Arrange - straight line from (0, 0) to (1, 0.5)
        var nodes = new List<BezierNode>
        {
            new(0f, 0f, new PointF(0f, 0f), new PointF(0.1f, 0f)),
            new(1f, 0.5f, new PointF(-0.1f, 0f), new PointF(0f, 0f))
        };

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 11);

        // Assert
        Assert.Equal(11, result.Length);
        // First sample should be at y=0
        Assert.Equal(0f, result[0], Tolerance);
        // Last sample should be at y=0.5
        Assert.Equal(0.5f, result[10], Tolerance);
    }

    [Fact]
    public void SampleToWaveTable_ReturnsCorrectSampleCount()
    {
        // Arrange
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 256);

        // Assert
        Assert.Equal(256, result.Length);
    }

    [Fact]
    public void SampleToWaveTable_AllSamplesWithinRange()
    {
        // Arrange
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 256);

        // Assert
        Assert.All(result, sample =>
        {
            Assert.True(sample >= -0.6f, $"Sample {sample} below -0.6");
            Assert.True(sample <= 0.6f, $"Sample {sample} above 0.6");
        });
    }

    [Fact]
    public void CreateSineWaveNodes_Returns5Nodes()
    {
        // Act
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Assert
        Assert.Equal(5, nodes.Count);
    }

    [Fact]
    public void CreateSineWaveNodes_FirstNodeAtOrigin()
    {
        // Act
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Assert
        Assert.Equal(0f, nodes[0].X);
        Assert.Equal(0f, nodes[0].Y);
    }

    [Fact]
    public void CreateSineWaveNodes_LastNodeAtEnd()
    {
        // Act
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Assert
        Assert.Equal(1f, nodes[^1].X);
        Assert.Equal(0f, nodes[^1].Y);
    }

    [Fact]
    public void CreateSineWaveNodes_HasCorrectPeakPositions()
    {
        // Act
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Assert
        // Peak at 0.25
        Assert.Equal(0.25f, nodes[1].X);
        Assert.Equal(0.5f, nodes[1].Y);
        
        // Zero crossing at 0.5
        Assert.Equal(0.5f, nodes[2].X);
        Assert.Equal(0f, nodes[2].Y);
        
        // Trough at 0.75
        Assert.Equal(0.75f, nodes[3].X);
        Assert.Equal(-0.5f, nodes[3].Y);
    }

    [Fact]
    public void SampleToWaveTable_SineWave_HasCorrectShape()
    {
        // Arrange
        var nodes = BezierWaveSampler.CreateSineWaveNodes();

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 100);

        // Assert
        // At start (index 0), should be near zero
        Assert.True(Math.Abs(result[0]) < 0.1f, $"Start should be near zero, was {result[0]}");
        
        // At quarter (index 25), should be positive peak
        Assert.True(result[25] > 0.3f, $"Quarter should be positive, was {result[25]}");
        
        // At half (index 50), should be near zero
        Assert.True(Math.Abs(result[50]) < 0.1f, $"Half should be near zero, was {result[50]}");
        
        // At three-quarter (index 75), should be negative
        Assert.True(result[75] < -0.3f, $"Three-quarter should be negative, was {result[75]}");
    }

    [Fact]
    public void SampleToWaveTable_NodesOutOfOrder_SortsAutomatically()
    {
        // Arrange - nodes in wrong order
        var nodes = new List<BezierNode>
        {
            new(1f, 0.5f),
            new(0f, 0f),
            new(0.5f, 0.25f)
        };

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 11);

        // Assert - should still produce reasonable output
        Assert.Equal(11, result.Length);
        Assert.Equal(0f, result[0], 0.1f); // First should be near 0
        Assert.Equal(0.5f, result[10], 0.1f); // Last should be near 0.5
    }

    [Fact]
    public void SampleToWaveTable_BeforeFirstNode_ReturnsFirstNodeValue()
    {
        // Arrange - first node not at x=0
        var nodes = new List<BezierNode>
        {
            new(0.2f, 0.3f),
            new(0.8f, 0.3f)
        };

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 11);

        // Assert - samples before first node should use first node's Y
        Assert.Equal(0.3f, result[0], 0.1f);
        Assert.Equal(0.3f, result[1], 0.1f);
    }

    [Fact]
    public void SampleToWaveTable_AfterLastNode_ReturnsLastNodeValue()
    {
        // Arrange - last node not at x=1
        var nodes = new List<BezierNode>
        {
            new(0.2f, 0.1f),
            new(0.5f, 0.4f)
        };

        // Act
        var result = BezierWaveSampler.SampleToWaveTable(nodes, 11);

        // Assert - samples after last node should use last node's Y
        Assert.Equal(0.4f, result[10], 0.1f);
    }
}

