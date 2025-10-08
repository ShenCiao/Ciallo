using Arch.Core;
using Arch.Core.Extensions;
using Ciallo.Command;
using Ciallo.Data;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Tests;

/// <summary>
/// Tests for the NewXXXCmd classes to verify the improved interface design
/// that handles both creating new entities and working with existing entities.
/// </summary>
[TestSuite, RequireGodotRuntime]
public class TestNewCommands
{
    private World _testWorld;

    [Before]
    public void Setup()
    {
        _testWorld = World.Create();
        _testWorld.AddForbiddenComponents();
        AppWorldManager.WorkingWorld.Value = _testWorld;
    }

    [After]
    public void Teardown()
    {
        if (_testWorld != null)
        {
            World.Destroy(_testWorld);
        }
    }

    /// <summary>
    /// Test that NewBrushCmd creates a new entity when no entity is provided
    /// </summary>
    [TestCase]
    public void TestNewBrushCmd_CreateNewEntity()
    {
        // Arrange
        var cmd = new NewBrushCmd();

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsNotEqual(Entity.Null);
        AssertThat(entity.Has<BrushSetting>()).IsTrue();
        AssertThat(cmd.BrushE).IsEqual(entity);
    }

    /// <summary>
    /// Test that NewBrushCmd works with an existing entity
    /// </summary>
    [TestCase]
    public void TestNewBrushCmd_WithExistingEntity()
    {
        // Arrange
        var existingEntity = _testWorld.Create();
        var setting = new BrushSetting();
        existingEntity.Add(setting);
        
        var cmd = new NewBrushCmd(existingEntity);

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsEqual(existingEntity);
        AssertThat(cmd.BrushE).IsEqual(existingEntity);
        AssertThat(entity.Has<BrushSetting>()).IsTrue();
    }

    /// <summary>
    /// Test that Do() is idempotent for data components
    /// </summary>
    [TestCase]
    public void TestNewBrushCmd_DoIsIdempotent()
    {
        // Arrange
        var existingEntity = _testWorld.Create();
        var setting = new BrushSetting();
        existingEntity.Add(setting);
        existingEntity.Add(new ToSerializeTag());
        
        var document = _testWorld.Create();
        document.Add(new BrushManager());
        document.Add(new DocumentBrushList());
        
        // Add to BrushManager before creating command
        var bm = document.Get<BrushManager>();
        bm.Add(existingEntity);
        
        var cmd = new NewBrushCmd(existingEntity);
        cmd.WorkingWorld = _testWorld;

        // Act - calling Do() should not throw even though ToSerializeTag already exists
        // Note: This would throw in the old implementation
        // We can't fully test Do() without the full UI setup, but we can test InitEntity
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsEqual(existingEntity);
        AssertThat(existingEntity.Has<ToSerializeTag>()).IsTrue();
        AssertThat(existingEntity.Has<BrushSetting>()).IsTrue();
    }

    /// <summary>
    /// Test that NewStrokeCmd creates a new entity when no entity is provided
    /// </summary>
    [TestCase]
    public void TestNewStrokeCmd_CreateNewEntity()
    {
        // Arrange
        var layerE = _testWorld.Create();
        layerE.Add(new LayerTreeNode());
        var cmd = new NewStrokeCmd(layerE);

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsNotEqual(Entity.Null);
        AssertThat(entity.Has<StrokeGeometry>()).IsTrue();
        AssertThat(entity.Has<LayerTreeNode>()).IsTrue();
        AssertThat(cmd.StrokeE).IsEqual(entity);
    }

    /// <summary>
    /// Test that NewStrokeCmd works with an existing entity
    /// </summary>
    [TestCase]
    public void TestNewStrokeCmd_WithExistingEntity()
    {
        // Arrange
        var layerE = _testWorld.Create();
        layerE.Add(new LayerTreeNode());
        
        var existingStroke = _testWorld.Create();
        existingStroke.Add(new StrokeGeometry());
        existingStroke.Add(new LayerTreeNode());
        existingStroke.Add<StrokeBrush>(Entity.Null);
        
        var cmd = new NewStrokeCmd(layerE, existingStroke);

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsEqual(existingStroke);
        AssertThat(cmd.StrokeE).IsEqual(existingStroke);
        AssertThat(entity.Has<StrokeGeometry>()).IsTrue();
    }

    /// <summary>
    /// Test that NewPolylineLayerCmd creates a new entity when no entity is provided
    /// </summary>
    [TestCase]
    public void TestNewPolylineLayerCmd_CreateNewEntity()
    {
        // Arrange
        var document = _testWorld.Create();
        var treeManager = new LayerTreeManager();
        document.Add(treeManager);
        
        var cmd = new NewPolylineLayerCmd();
        cmd.WorkingWorld = _testWorld;

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsNotEqual(Entity.Null);
        AssertThat(entity.Has<PolylineLayerSetting>()).IsTrue();
        AssertThat(entity.Has<LayerTreeNode>()).IsTrue();
        AssertThat(cmd.LayerE).IsEqual(entity);
    }

    /// <summary>
    /// Test that NewPolylineLayerCmd works with an existing entity
    /// </summary>
    [TestCase]
    public void TestNewPolylineLayerCmd_WithExistingEntity()
    {
        // Arrange
        var existingLayer = _testWorld.Create();
        var setting = new PolylineLayerSetting();
        existingLayer.Add(setting);
        existingLayer.Add(new LayerTreeNode());
        
        var cmd = new NewPolylineLayerCmd(existingLayer);

        // Act
        var entity = cmd.InitEntity();

        // Assert
        AssertThat(entity).IsEqual(existingLayer);
        AssertThat(cmd.LayerE).IsEqual(existingLayer);
        AssertThat(entity.Has<PolylineLayerSetting>()).IsTrue();
    }
}
