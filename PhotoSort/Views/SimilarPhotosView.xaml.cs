using System.Windows;
using System.Windows.Input;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class SimilarPhotosView : System.Windows.Controls.UserControl
{
    public SimilarPhotosView()
    {
        InitializeComponent();
    }

    private void OnGroupClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SimilarPhotoGroup group
            && DataContext is SimilarPhotosViewModel viewModel)
        {
            var index = viewModel.SimilarGroups.IndexOf(group);
            if (index >= 0)
            {
                viewModel.SelectedGroupIndex = index;
            }
        }
    }
}
