using Godot;
using Humanizer;

namespace Ciallo.GuiControl;

public partial class ShortcutSettings : AcceptDialog
{
    private static readonly StringName ClearActionMetadataMethod = "clear_action_metadata";
    private static readonly StringName RegisterGroupMethod = "register_group";
    private static readonly StringName RegisterActionMethod = "register_action";
    private static readonly StringName ConfigureRemappingMethod = "configure_remapping";

    public override void _EnterTree()
    {
        var keychain = GetNode<Node>("/root/Keychain");
        keychain.Call(ClearActionMetadataMethod);

        foreach (string group in GeneratedShortcutMetadata.Groups)
            keychain.Call(RegisterGroupMethod, GroupLabel(group), "", false);

        foreach (var action in GeneratedShortcutMetadata.Actions)
        {
            keychain.Call(
                RegisterActionMethod,
                action.ActionName,
                action.ActionSegment.Humanize(LetterCasing.Title),
                GroupLabel(action.GroupName),
                action.Global);
        }

        keychain.Call(ConfigureRemappingMethod, true, false, true, true, false);
    }

    private static string GroupLabel(string group) => group.Split('.')[1].Humanize(LetterCasing.Title);

    public override void _Ready()
    {
        Title = "Shortcuts";
        OkButtonText = "Close";
    }
}
