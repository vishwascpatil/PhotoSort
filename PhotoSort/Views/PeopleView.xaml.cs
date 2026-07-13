using System.Windows.Input;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class PeopleView : System.Windows.Controls.UserControl
{
    public PeopleView()
    {
        InitializeComponent();
    }

    private void OnPersonClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && element.DataContext is PersonInfo person
            && DataContext is PeopleViewModel viewModel)
        {
            var index = viewModel.People.IndexOf(person);
            if (index >= 0)
            {
                viewModel.SelectedPersonIndex = index;
            }
        }
    }

    private void OnFaceClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && element.DataContext is FaceInfo face
            && DataContext is PeopleViewModel viewModel)
        {
            if (viewModel.IsSplitMode)
            {
                viewModel.ToggleFaceSplitSelectionCommand.Execute(face);
            }
        }
    }
}
