using System.Threading.Tasks;
using Godot;

namespace Ciallo.GuiControl;

public partial class SaveChangeDialog : ConfirmationDialog
{
    private TaskCompletionSource<int> _dialogResultSource;
    public readonly Button YesButton;
    public readonly Button NoButton;

    public SaveChangeDialog()
    {
        DialogText = "Save changes?";
        NoButton = AddButton("No");
        YesButton = AddButton("Yes");
        GetOkButton().Visible = false;

        YesButton.Pressed += OnYes;
        NoButton.Pressed += OnNo;
        Canceled += OnCancel;
    }

    public Task<int> PopupCollectInput()
    {
        _dialogResultSource = new TaskCompletionSource<int>();
        Popup();

        return _dialogResultSource.Task;
    }

    private void OnNo()
    {
        _dialogResultSource?.SetResult(0);
        _dialogResultSource = null;
        Hide();
    }

    public void OnYes()
    {
        _dialogResultSource?.SetResult(1);
        _dialogResultSource = null;
        Hide();
    }

    public void OnCancel()
    {
        _dialogResultSource?.SetResult(-1);
        _dialogResultSource = null;
        Hide();
    }
}
