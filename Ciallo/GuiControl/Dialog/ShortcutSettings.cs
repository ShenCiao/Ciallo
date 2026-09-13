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
            keychain.Call(RegisterGroupMethod, group, "", false);

        foreach (var action in GeneratedShortcutMetadata.Actions)
        {
            keychain.Call(
                RegisterActionMethod,
                action.ActionName,
                action.ActionSegment.Humanize(LetterCasing.Title),
                action.GroupName,
                action.Global);
        }

        keychain.Call(ConfigureRemappingMethod, true, false, true, true, false);
    }

    public override void _Ready() => UpdateTranslation();

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationTranslationChanged && IsNodeReady()) UpdateTranslation();
    }

    private void UpdateTranslation()
    {
        Title = Tr("Shortcuts");
        OkButtonText = Tr("Close");
    }
}
