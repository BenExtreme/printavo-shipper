namespace PrintavoShipper.App.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
}
