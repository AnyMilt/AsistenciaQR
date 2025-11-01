using AsistenciaQR.Models;
using AsistenciaQR.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;


namespace AsistenciaQR.ViewModels;

public class HistorialViewModel : ObservableObject
{
    public ObservableCollection<RegistroAsistencia> Registros { get; } = new();
    public int Pendientes => Registros.Count(r => !r.Sincronizado);
    public int Sincronizados => Registros.Count(r => r.Sincronizado);

    public ICommand EliminarRegistroCommand { get; }
    public ICommand EliminarTodoCommand { get; }
    public ICommand SincronizarAhoraCommand { get; }
    public ICommand EliminarPendientesCommand { get; }
    public ICommand MostrarPendientesCommand { get; }



    public HistorialViewModel()
    {
        EliminarRegistroCommand = new AsyncRelayCommand<RegistroAsistencia>(EliminarRegistroAsync);
        EliminarTodoCommand = new AsyncRelayCommand(EliminarTodoAsync);
        SincronizarAhoraCommand = new Command(async () => await SincronizarAhoraAsync());
        EliminarPendientesCommand = new Command(async () => await EliminarPendientesAsync());
        MostrarPendientesCommand = new Command(async () => await CargarHistorialAsync());

    }
    
    private async Task EliminarPendientesAsync()
    {
        await DB.InitAsync();
        var pendientes = await DB.ObtenerPendientesAsync();

        foreach (var registro in pendientes)
        {
            await DB.EliminarAsync(registro.Id);
        }

        await CargarHistorialAsync(); // refresca la vista
    }

    public async Task CargarHistorialAsync()
    {
        await DB.InitAsync();
        var registros = await DB.ObtenerTodosAsync(); // método que devuelve todos los registros

        Registros.Clear();
        foreach (var r in registros
        .Where(r => !r.Sincronizado) // solo los no sincronizados
        .OrderByDescending(r => r.FechaEscaneo))
        {
            Registros.Add(r);
        }


        OnPropertyChanged(nameof(Pendientes));
        OnPropertyChanged(nameof(Sincronizados));

    }

    private async Task EliminarRegistroAsync(RegistroAsistencia registro)
    {
        bool confirmado = await Shell.Current.DisplayAlert(
            "Eliminar registro",
            $"¿Deseas eliminar el registro del {registro.FechaEscaneo:dd/MM/yyyy HH:mm}?",
            "Sí", "Cancelar");

        if (!confirmado)
            return;

        await DB.EliminarAsync(registro.Id);
        Registros.Remove(registro);

        OnPropertyChanged(nameof(Pendientes));
        OnPropertyChanged(nameof(Sincronizados));
    }

    private async Task EliminarTodoAsync()
    {
        if (!Registros.Any())
        {
            await Shell.Current.DisplayAlert("Sin registros", "No hay registros para eliminar.", "OK");
            return;
        }

        bool confirmado = await Shell.Current.DisplayAlert(
            "Eliminar historial",
            "¿Estás seguro de que deseas eliminar todos los registros?",
            "Sí", "Cancelar");

        if (!confirmado)
            return;

        await DB.EliminarTodosAsync();
        Registros.Clear();

        OnPropertyChanged(nameof(Pendientes));
        OnPropertyChanged(nameof(Sincronizados));
    }

    private async Task SincronizarAhoraAsync()
    {
        await DB.InitAsync();
        var pendientes = await DB.ObtenerPendientesAsync();

        foreach (var registro in pendientes)
        {
            if (Uri.TryCreate(registro.UrlEscaneo, UriKind.Absolute, out var uri))
            {
                if (await IntentarRegistroEnLinea(uri.ToString()))
                {
                    var fecha = DateTime.Now.Date;
                    var hora = DateTime.Now.TimeOfDay;

                    await DB.MarcarSincronizadoAsync(registro.Id);
                }
            }
        }

        await CargarHistorialAsync(); // refresca la vista
    }

    private async Task<bool> IntentarRegistroEnLinea(string url)
    {
        try
        {
            using var client = new HttpClient();
            var respuesta = await client.GetAsync(url);
            return respuesta.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }


    public ICommand ExportarJsonCommand => new AsyncRelayCommand(ExportarJsonAsync);

    private async Task ExportarJsonAsync()
    {
        var exportador = new ExportadorAsistencia();
        var ruta = await exportador.ExportarYCompartirAsync();

        if (ruta == null)
        {
            await Shell.Current.DisplayAlert("Sin registros", "No hay registros pendientes para exportar.", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Exportación completada", $"Archivo guardado en:\n{ruta}", "OK");
        }

    }

}
