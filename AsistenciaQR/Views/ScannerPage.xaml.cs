using AsistenciaQR.ViewModels;
using AsistenciaQR.Services;
using SQLite;
using ZXing.Net.Maui;

namespace AsistenciaQR.Views;

public partial class ScannerPage : ContentPage
{
    private bool procesandoCodigo = false;
    private string ultimoCodigo = string.Empty;

    public ScannerPage()
	{
		InitializeComponent();
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "asistencia.db3");
        var connection = new SQLiteAsyncConnection(dbPath);

        BindingContext = new ScannerViewModel(
            new LocalStorageService(),
            new SyncService(connection)
        );

    }
    private void OnBarcodeDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results.Count() == 0)
            return;

        var codigo = e.Results[0].Value;
        var viewModel = BindingContext as ScannerViewModel;

        // Delega el control al ViewModel
        if (viewModel?.EscaneoActivo == true && !viewModel.ProcesandoCodigo)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                viewModel.ProcesarCodigoCommand.Execute(codigo);
                await MostrarCheckAnimacionAsync();
            });
        }
    }

    private async Task MostrarCheckAnimacionAsync()
    {
        CheckAnimacion.Opacity = 0;
        CheckAnimacion.Scale = 0.5;
        CheckAnimacion.TranslationY = 50;

        await CheckAnimacion.FadeTo(1, 200);
        await CheckAnimacion.ScaleTo(1.2, 200, Easing.CubicOut);
        await CheckAnimacion.TranslateTo(0, -30, 300, Easing.CubicOut);

        await Task.Delay(800);

        await CheckAnimacion.FadeTo(0, 300);
        await CheckAnimacion.TranslateTo(0, 50, 300, Easing.CubicIn);
        CheckAnimacion.Scale = 0.5;
    }

}