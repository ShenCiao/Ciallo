using System.Collections.Immutable;
using System.Linq;
using Ciallo.Data;
using Frent;

namespace Ciallo.Command;

[CommandBuilder]
public class SelectLayersCmd : CommandBase
{
    private readonly bool _recordCelSelectionPreference;
    private Entity _celSelectionPreferenceFolder = Entity.Null;
    private ImmutableArray<string> _oldPreferredNames;
    private ImmutableArray<string> _newPreferredNames;
    private ImmutableArray<Entity> _oldLayers;
    private ImmutableArray<Entity> _newLayers;

    public SelectLayersCmd(bool recordCelSelectionPreference = false, ImmutableArray<Entity> layers = default)
    {
        _recordCelSelectionPreference = recordCelSelectionPreference;
        _newLayers = layers;
    }

    public override void BeforeFirstDo(Entity newLayerE)
    {
        var sm = Document.Get<SelectionManager>();
        _oldLayers = sm.SelectedLayers.Value;
        if (_newLayers.IsDefault)
            _newLayers = newLayerE.IsDocument ? [] : [newLayerE];
        if (_newLayers.IsEmpty)
            return;
        newLayerE = _newLayers[0];

        if (!_recordCelSelectionPreference)
            return;

        var cel = newLayerE.Get<LayerTreeNode>().ParentValue;
        if (cel.IsNull || !cel.Tagged<CelTag>())
            return;
        _celSelectionPreferenceFolder = cel.Get<LayerTreeNode>().ParentValue;
        _newPreferredNames = CelLayerSelection.Names(
            [.. _newLayers.Where(layer => layer.Get<LayerTreeNode>().ParentValue == cel)]);
        _oldPreferredNames = _celSelectionPreferenceFolder
            .Get<FolderLayerSetting>().PreferredNamesForCelSelection.Value;
    }

    public override void Do(Entity newLayerE)
    {
        SetCelSelectionPreferenceNames(_newPreferredNames);

        Document.Get<SelectionManager>().SelectedLayers.Value = _newLayers;
    }

    public override void Undo(Entity newLayerE)
    {
        SetCelSelectionPreferenceNames(_oldPreferredNames);
        Document.Get<SelectionManager>().SelectedLayers.Value = _oldLayers;
    }

    private void SetCelSelectionPreferenceNames(ImmutableArray<string> names)
    {
        if (!_recordCelSelectionPreference
            || _celSelectionPreferenceFolder.IsNull)
            return;

        _celSelectionPreferenceFolder
            .Get<FolderLayerSetting>()
            .PreferredNamesForCelSelection
            .Value = names;
    }
}
