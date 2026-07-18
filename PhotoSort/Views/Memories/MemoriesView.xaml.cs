using System.Windows;
using System.Windows.Controls;
using PhotoSort.ViewModels;

namespace PhotoSort.Views.Memories;

public partial class MemoriesView : UserControl
{
    private bool _isFirstLoad = true;

    public MemoriesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isFirstLoad)
        {
            _isFirstLoad = false;
            if (DataContext is MemoriesViewModel vm)
            {
                _ = vm.LoadInitialCommand.ExecuteAsync(null);
            }
        }
    }
}
