using AsistenciaQR.Models;
using AsistenciaQR.Services;
using AsistenciaQR.ViewModels;
using AsistenciaQR.Views;

namespace AsistenciaQR
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Connectivity.ConnectivityChanged += async (_, args) =>
            {
                var vm = new ScannerViewModel(new LocalStorageService(), new SyncService(DB.Conexion));
                await vm.SincronizarSiDisponibleAsync();
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}