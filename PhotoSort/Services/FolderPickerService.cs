using Microsoft.Win32;

namespace PhotoSort.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder to import",
            Multiselect = false,
            ValidateNames = true
        };

        if (!string.IsNullOrEmpty(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
