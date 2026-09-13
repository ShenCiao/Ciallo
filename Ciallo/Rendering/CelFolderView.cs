using System;
using System.Collections.Generic;
using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.Rendering;

public partial class CelFolderView : FolderLayerView
{
    private readonly List<Node2D> _displayingOnionSkinViews = [];

    private Node2D DisplayingLayerView
    {
        get;
        set
        {
            if (field != null && field != value) HideLayerView(field);
            field = value;
            if (field != null) ShowCurrentLayerView(field);
        }
    }

    public CompositeDisposable Observe(
        FolderLayerSetting setting,
        LayerTreeNode layerNode,
        Observable<bool> shouldShowOnionSkin,
        Observable<SortedList<int, ShaderMaterial>> onionSkinMaterials)
    {
        CompositeDisposable subs = new();
        layerNode.ObserveAddChild().Subscribe(et =>
        {
            HideLayerView(GetLayerView(et.Value));
        }).AddTo(subs);

        layerNode.ObserveRemoveChild().Subscribe(et =>
        {
            ShowCurrentLayerView(GetLayerView(et.Value));
        }).AddTo(subs);

        // Note: Although CurrentOnionSkinCels could have duplicated entities (e.g. if the same cel is onion-skinned at multiple offsets)
        // Its Ok to ShowOnionSkin it twice
        setting.CurrentExposedCel.CombineLatest(shouldShowOnionSkin, setting.CurrentOnionSkinCels, onionSkinMaterials, ValueTuple.Create)
            .Subscribe(tuple =>
            {
                var (currentCel, shouldShow, onionSkinCels, materials) = tuple;
                DisplayingLayerView = currentCel.IsNull || currentCel.IsCelFolder ? null : GetLayerView(currentCel);
                HideDisplayingOnionSkinViews();

                if (!shouldShow)
                    return;

                foreach (var (offset, cel) in onionSkinCels)
                {
                    if (cel.IsCelFolder)
                        continue;
                    if (!materials.TryGetValue(offset, out var material))
                        continue;

                    var view = GetLayerView(cel);
                    if (view == DisplayingLayerView)
                        continue;

                    ShowOnionSkin(view, material, offset);
                    _displayingOnionSkinViews.Add(view);
                }
            }).AddTo(subs);

        return subs;
    }

    private void HideDisplayingOnionSkinViews()
    {
        foreach (var view in _displayingOnionSkinViews)
            HideOnionSkin(view);
        _displayingOnionSkinViews.Clear();
        if (DisplayingLayerView != null)
            ShowCurrentLayerView(DisplayingLayerView);
    }

    private static void HideLayerView(Node2D node) => node.VisibilityLayer = 0;
    private static void ShowCurrentLayerView(Node2D node)
    {
        node.VisibilityLayer = (uint)AppGodotLayers.Render2DLayer.View;
        node.Material = null;
        node.ZIndex = 0;
    }

    private static void ShowOnionSkin(Node2D node, ShaderMaterial material, int offset)
    {
        node.VisibilityLayer = (uint)AppGodotLayers.Render2DLayer.Other;
        node.Material = material;
        node.ZIndex = offset > 0 ? -1 : -2;
    }

    private static void HideOnionSkin(Node2D node)
    {
        node.VisibilityLayer = 0;
        node.Material = null;
        node.ZIndex = 0;
    }

    private static Node2D GetLayerView(Entity e)
    {
        if (e.Has<ShapeLayerView>())
            return e.Get<ShapeLayerView>();
        if (e.Has<FolderLayerView>())
            return e.Get<FolderLayerView>();
        if (e.Has<Sprite2D>())
            return e.Get<Sprite2D>();

        throw new Exception("Unknown layer type");
    }
}
