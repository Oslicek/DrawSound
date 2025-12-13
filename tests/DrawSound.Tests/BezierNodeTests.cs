using DrawSound.Core.Audio;

namespace DrawSound.Tests;

public class BezierNodeTests
{
    [Fact]
    public void Constructor_WithXY_SetsPositionCorrectly()
    {
        // Arrange & Act
        var node = new BezierNode(0.5f, 0.25f);

        // Assert
        Assert.Equal(0.5f, node.X);
        Assert.Equal(0.25f, node.Y);
    }

    [Fact]
    public void Constructor_WithXY_SetsDefaultHandles()
    {
        // Arrange & Act
        var node = new BezierNode(0.5f, 0.25f);

        // Assert - default handles are horizontal
        Assert.Equal(-0.05f, node.HandleIn.X);
        Assert.Equal(0f, node.HandleIn.Y);
        Assert.Equal(0.05f, node.HandleOut.X);
        Assert.Equal(0f, node.HandleOut.Y);
    }

    [Fact]
    public void Constructor_WithHandles_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var handleIn = new PointF(-0.1f, 0.2f);
        var handleOut = new PointF(0.1f, -0.2f);

        // Act
        var node = new BezierNode(0.5f, 0.25f, handleIn, handleOut);

        // Assert
        Assert.Equal(0.5f, node.X);
        Assert.Equal(0.25f, node.Y);
        Assert.Equal(-0.1f, node.HandleIn.X);
        Assert.Equal(0.2f, node.HandleIn.Y);
        Assert.Equal(0.1f, node.HandleOut.X);
        Assert.Equal(-0.2f, node.HandleOut.Y);
    }

    [Fact]
    public void GetHandleInAbsolute_ReturnsCorrectPosition()
    {
        // Arrange
        var node = new BezierNode(0.5f, 0.3f, new PointF(-0.1f, 0.05f), new PointF(0.1f, -0.05f));

        // Act
        var absPos = node.GetHandleInAbsolute();

        // Assert
        Assert.Equal(0.4f, absPos.X, 4);
        Assert.Equal(0.35f, absPos.Y, 4);
    }

    [Fact]
    public void GetHandleOutAbsolute_ReturnsCorrectPosition()
    {
        // Arrange
        var node = new BezierNode(0.5f, 0.3f, new PointF(-0.1f, 0.05f), new PointF(0.1f, -0.05f));

        // Act
        var absPos = node.GetHandleOutAbsolute();

        // Assert
        Assert.Equal(0.6f, absPos.X, 4);
        Assert.Equal(0.25f, absPos.Y, 4);
    }

    [Fact]
    public void MirrorHandles_MirrorToIn_CreatesSymmetricHandles()
    {
        // Arrange
        var node = new BezierNode(0.5f, 0.0f);
        node.HandleOut = new PointF(0.1f, 0.2f);

        // Act
        node.MirrorHandles(mirrorToIn: true);

        // Assert
        Assert.Equal(-0.1f, node.HandleIn.X, 4);
        Assert.Equal(-0.2f, node.HandleIn.Y, 4);
    }

    [Fact]
    public void MirrorHandles_MirrorToOut_CreatesSymmetricHandles()
    {
        // Arrange
        var node = new BezierNode(0.5f, 0.0f);
        node.HandleIn = new PointF(-0.15f, 0.1f);

        // Act
        node.MirrorHandles(mirrorToIn: false);

        // Assert
        Assert.Equal(0.15f, node.HandleOut.X, 4);
        Assert.Equal(-0.1f, node.HandleOut.Y, 4);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new BezierNode(0.5f, 0.3f, new PointF(-0.1f, 0.1f), new PointF(0.1f, -0.1f));

        // Act
        var clone = original.Clone();
        original.X = 0.9f;
        original.Y = 0.9f;

        // Assert
        Assert.Equal(0.5f, clone.X);
        Assert.Equal(0.3f, clone.Y);
        Assert.Equal(-0.1f, clone.HandleIn.X);
        Assert.Equal(0.1f, clone.HandleOut.X);
    }
}

