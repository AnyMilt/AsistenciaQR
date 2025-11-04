using Microsoft.Maui.Storage;
namespace AsistenciaQR.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage()
	{
		InitializeComponent();

        CargarConfiguracion();
    }
    private void CargarConfiguracion()
    {
        // Cargar valores almacenados en Preferences (si existen)
        MinutosQRInput.Text = Preferences.Get("MinutosQRValidez", "10");
        ServidorInput.Text = Preferences.Get("ServidorURL", "https://mi-servidor.com/asistencia");
    }

    private async void Guardar_Clicked(object sender, EventArgs e)
    {
        // Guardar valores en Preferences
        Preferences.Set("MinutosQRValidez", MinutosQRInput.Text);
        Preferences.Set("ServidorURL", ServidorInput.Text);

        await DisplayAlert("✅ Guardado", "Configuración actualizada correctamente.", "OK");
    }

    private async void Restablecer_Clicked(object sender, EventArgs e)
    {
        bool confirmar = await DisplayAlert("Restablecer", "¿Deseas restablecer los valores por defecto?", "Sí", "No");
        if (!confirmar)
            return;

        Preferences.Set("TiempoCaducidad", 10);
        Preferences.Set("ServidorURL", "https://invincibly-peachy-tyrone.ngrok-free.dev/asistencia/registrar");

        CargarConfiguracion();
        await DisplayAlert("♻️ Restablecido", "Valores predeterminados restaurados.", "OK");
    }
}