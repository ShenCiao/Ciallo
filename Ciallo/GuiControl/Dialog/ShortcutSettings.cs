using Godot;

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

        RegisterGroup(keychain, "File");
        RegisterGroup(keychain, "Edit");
        RegisterGroup(keychain, "Tool");
        RegisterGroup(keychain, "Interaction");

        RegisterAction(keychain, "NewDocument", "New Document", "File");
        RegisterAction(keychain, "OpenDocument", "Open Document", "File");
        RegisterAction(keychain, "Save", "Save", "File");
        RegisterAction(keychain, "SaveAs", "Save As", "File");
        RegisterAction(keychain, "Undo", "Undo", "Edit");
        RegisterAction(keychain, "Redo", "Redo", "Edit");
        RegisterAction(keychain, "Copy", "Copy", "Edit");
        RegisterAction(keychain, "Cut", "Cut", "Edit");
        RegisterAction(keychain, "Paste", "Paste", "Edit");
        RegisterAction(keychain, "Delete", "Delete", "Edit");
        RegisterAction(keychain, "ToolSelection", "Selection Tool", "Tool");
        RegisterAction(keychain, "ToolPaintBrush", "Paint Brush", "Tool");
        RegisterAction(keychain, "ToolPaintFill", "Paint Fill", "Tool");
        RegisterAction(keychain, "CancelInteraction", "Cancel", "Interaction");
        RegisterAction(keychain, "ConfirmInteraction", "Confirm", "Interaction");

        keychain.Call(ConfigureRemappingMethod, true, false, false, false, false);
    }

    public override void _Ready() => UpdateTranslation();

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationTranslationChanged && IsNodeReady()) UpdateTranslation();
    }

    private static void RegisterGroup(Node keychain, StringName group) =>
        keychain.Call(RegisterGroupMethod, group, "", false);

    private static void RegisterAction(Node keychain, StringName action, string displayName, StringName group) =>
        keychain.Call(RegisterActionMethod, action, displayName, group, true);

    private void UpdateTranslation()
    {
        Title = Tr("Shortcuts");
        OkButtonText = Tr("Close");
    }
}
