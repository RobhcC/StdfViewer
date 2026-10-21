namespace StdfViewer.Services
{
    public interface IDialogService
    {
        string PickStdfFile();
        bool Confirm(string title, string message);
    }
}
