using AsistenciaQR.ViewModels;

namespace AsistenciaQR.Views;

public partial class HistorialPage : ContentPage
{
    private readonly HistorialViewModel viewModel;

    public HistorialPage()
    {
        InitializeComponent();
        viewModel = new HistorialViewModel();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.CargarHistorialAsync();
    }

}