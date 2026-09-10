using System.Collections.Immutable;
using Ciallo.Data;
using Frent;

namespace Ciallo.Command;

[CommandBuilder]
public class SetLayerSelectionCmd : CommandBase
{
    private readonly bool _recordCelSelectionPreference;
    private Entity _celSelectionPreferenceFolder = Entity.Null;
    private string _oldPreferredName;
    private string _newPreferredName;
    private ImmutableArray<Entity> _oldLayers;
    private ImmutableArray<Entity> _newLayers;

    public SetLayerSelectionCmd(bool recordCelSelectionPreference = false, ImmutableArray<Entity> layers = default)
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

        if (!TryGetCelSelectionPreferenceTarget(newLayerE, out _celSelectionPreferenceFolder, out _newPreferredName))
            return;

        _oldPreferredName = _celSelectionPreferenceFolder
            .Get<FolderLayerSetting>().PreferredNameForCelSelection.Value;
    }

    public override void Do(Entity newLayerE)
    {
        SetCelSelectionPreferenceName(_newPreferredName);

        Document.Get<SelectionManager>().SelectedLayers.Value = _newLayers;
    }

    public override void Undo(Entity newLayerE)
    {
        SetCelSelectionPreferenceName(_oldPreferredName);
        Document.Get<SelectionManager>().SelectedLayers.Value = _oldLayers;
    }

    private void SetCelSelectionPreferenceName(string name)
    {
        if (!_recordCelSelectionPreference
            || _celSelectionPreferenceFolder.IsNull
            || !_celSelectionPreferenceFolder.IsAlive)
            return;

        _celSelectionPreferenceFolder
            .Get<FolderLayerSetting>()
            .PreferredNameForCelSelection
            .Value = name;
    }

    /// <summary>
    /// Determines whether switching to <paramref name="newLayerE"/> should update a cel folder's
    /// preferred cel child name, and to what. Only a direct child inside a cel
    /// qualifies; in every other case (the cel folder itself, the cel root, or a deeper nested
    /// layer) the preference is left untouched.
    /// </summary>
    private static bool TryGetCelSelectionPreferenceTarget(
        Entity newLayerE,
        out Entity celFolder,
        out string name)
    {
        celFolder = Entity.Null;
        name = null;

        if (newLayerE.IsNull || newLayerE.IsDocument || !newLayerE.IsAlive)
            return false;

        if (newLayerE.TryGet<FolderLayerSetting>()?.IsCelFolder == true)
            return false;

        // The preference only tracks direct children inside a cel: the new layer's parent must be a cel.
        var celE = newLayerE.Get<LayerTreeNode>().ParentValue;
        if (celE.IsNull || celE.IsDocument || !celE.Tagged<CelTag>())
            return false;

        celFolder = celE.Get<LayerTreeNode>().ParentValue;
        name = newLayerE.Get<CommonLayerSetting>().Name.Value;
        return true;
    }
}
