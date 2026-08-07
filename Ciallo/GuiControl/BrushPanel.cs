using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Ciallo.Data;
using Ciallo.Widget;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

public partial class BrushPanel : AcceptDialog
{
    public readonly ReactiveProperty<int> SelectedIndex = new(-1);

    public ItemList BrushSelector;
    public Container PropertiesHolder;
    public Button Add;
    public Button Remove;
    public Button Copy;
    public Button Reset;
    public Button Top;
    public Button Up;
    public Button Down;
    public Button Bottom;
    public Container Operators;

    public SubViewport BrushPreviewViewport;
    public SubViewportContainer BrushPreviewContainer;
    public Camera2D Camera;
    public float PreviewBaseWidth = Single.Pi * (2f + 0.3f); // 2pi + padding blank
    public const float PreviewAspectRatio = 1.618f * 2;

    [OnInstantiate]
    private void Initialise() { }

    public override void _EnterTree()
    {
        BrushSelector = GetNode<ItemList>("%BrushSelector");
        PropertiesHolder = GetNode<Container>("%PropertiesHolder");
        Add = GetNode<Button>("%Add");
        Remove = GetNode<Button>("%Remove");
        Copy = GetNode<Button>("%Copy");
        Reset = GetNode<Button>("%Reset");
        Top = GetNode<Button>("%Top");
        Up = GetNode<Button>("%Up");
        Down = GetNode<Button>("%Down");
        Bottom = GetNode<Button>("%Bottom");
        Operators = GetNode<Container>("%Operators");

        BrushPreviewViewport = GetNode<SubViewport>("%BrushPreviewViewport");
        BrushPreviewContainer = BrushPreviewViewport.GetParent<SubViewportContainer>();
        Camera = BrushPreviewViewport.GetChild<Camera2D>(0);
    }

    public override void _Ready()
    {
        GetOkButton().Visible = false;

        var background = (GradientTexture2D)BrushPreviewViewport.GetChild<Sprite2D>(1).Texture;
        background.Width = (int)Mathf.Ceil(PreviewBaseWidth);
        background.Height = (int)Mathf.Ceil(PreviewBaseWidth / PreviewAspectRatio);

        BrushPreviewContainer.Resized += () =>
        {
            Vector2 size = BrushPreviewContainer.Size;
            if ((MathF.Abs(size.X / size.Y - PreviewAspectRatio) <= 1e-5f)) return;

            size.Y = size.X / PreviewAspectRatio;
            BrushPreviewContainer.CustomMinimumSize = new(0, size.Y);
        };

        BrushPreviewViewport.SizeChanged += () =>
        {
            var size = BrushPreviewContainer.Size;
            var zoomLevel = size.X / PreviewBaseWidth;
            Camera.Zoom = Vector2.One * zoomLevel;
        };

        BrushPreviewContainer.ResetSize();

        var _isPanning = false;
        BrushPreviewContainer.GuiInput += et =>
        {
            if (et is InputEventMouseMotion button && _isPanning) Camera.Offset -= button.Relative / Camera.Zoom;

            // Drag middle mouse to pan
            if (et is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: true }) _isPanning = true;
            if (et is InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: false }) _isPanning = false;
            // Double click to reset camera position.
            if (et is InputEventMouseButton { ButtonIndex: MouseButton.Middle, DoubleClick: true })
            {
                Camera.Offset = Vector2.Zero;
                var size = BrushPreviewContainer.Size;
                var zoomLevel = size.X / PreviewBaseWidth;
                Camera.Zoom = Vector2.One * zoomLevel;
            }
            var zoomFactor = AppPreference.MouseWheelZoomFactor.Value;
            if (et is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp })
            {
                Camera.Zoom *= 1.0f + zoomFactor;
            }
            else if (et is InputEventMouseButton { ButtonIndex: MouseButton.WheelDown })
            {
                Camera.Zoom *= 1.0f - zoomFactor;
            }
        };

        AppStrokeBrushLibrary.BindToGui(this);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Propagate unhandled key event to main viewport to enable shortcuts
        // Only work for godot `Window` with `exclusive` false
        if (@event is InputEventKey)
        {
            GetParent().GetViewport().PushInput(@event);
        }
    }

    public void BindBrushSetting<T>(
        ObservableList<T> list,
        Func<T, StrokeBrushSetting> toBrushSetting)
    {
        BrushSelector.ObserveObservableList(list, e => toBrushSetting(e).Name)
            .BindSelectionIndex(SelectedIndex);

        foreach (var item in list)
        {
            var propertyBox = new PropertyContainer()
                .VisibleIf(SelectedIndex, idx => EqualityComparer<T>.Default.Equals(list.ElementAtOrDefault(idx), item));
            toBrushSetting(item).DrawProperty(propertyBox);
            PropertiesHolder.AddChild(propertyBox);
        }

        list.ObserveChanged().Subscribe(e =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    var propertyBox = new PropertyContainer()
                        .VisibleIf(SelectedIndex, idx => EqualityComparer<T>.Default.Equals(list.ElementAtOrDefault(idx), e.NewItem));
                    toBrushSetting(e.NewItem).DrawProperty(propertyBox);
                    PropertiesHolder.AddChild(propertyBox);
                    PropertiesHolder.MoveChild(propertyBox, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    PropertiesHolder.GetChild(e.OldStartingIndex).QueueFree();
                    break;
                case NotifyCollectionChangedAction.Move:
                    PropertiesHolder.MoveNode([e.OldStartingIndex], [e.NewStartingIndex]);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    PropertiesHolder.QueueFreeChildren();
                    break;
                case NotifyCollectionChangedAction.Replace:
                    throw new("Replace action is not supported");
            }
        }).AddTo(this);
    }
}
