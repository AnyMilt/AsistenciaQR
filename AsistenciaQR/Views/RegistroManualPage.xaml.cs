using AsistenciaQR.ViewModels;

namespace AsistenciaQR.Views
{
    public partial class RegistroManualPage : ContentPage
    {
        public RegistroManualPage()
        {
            InitializeComponent();
            BindingContext = new RegistroManualViewModel();
        }

        private void BusquedaInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BindingContext is RegistroManualViewModel vm)
            {
                vm.FiltroBusqueda = e.NewTextValue;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is RegistroManualViewModel vm)
            {
                await vm.CargarDocentes();
            }
        }

    }
}
