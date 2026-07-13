using System.Windows;
using System.Windows.Controls;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class DuplicateDetectionView : UserControl
{
    public DuplicateDetectionView()
    {
        InitializeComponent();
    }

    private DuplicateDetectionViewModel? ViewModel => DataContext as DuplicateDetectionViewModel;

    private void OnGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && ViewModel is not null)
        {
            ViewModel.SelectedGroupIndex = listBox.SelectedIndex;
        }
    }
}
