using System.Windows;
using System.Windows.Input;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class CleanupView : System.Windows.Controls.UserControl
{
    public CleanupView()
    {
        InitializeComponent();
    }

    private void OnCategoryClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CleanupCategory category
            && DataContext is CleanupViewModel viewModel)
        {
            viewModel.SelectCategoryCommand.Execute(category);
        }
    }

    private void OnPhotoClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GalleryPhoto photo
            && DataContext is CleanupViewModel viewModel)
        {
            viewModel.TogglePhotoSelectionCommand.Execute(photo);
        }
    }
}
