using AsistenciaQR.Models;
using Newtonsoft.Json;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace AsistenciaQR.Services;

public class ExportadorAsistencia
{
    public async Task<string?> ExportarYCompartirAsync()
    {
        var registros = await ObtenerPendientesAsync();

        if (registros.Count == 0)
            return null;

        var exportables = registros.Select(r => new RegistroExportado
        {
            UrlEscaneo = r.UrlEscaneo,
            Fecha = r.FechaEscaneo.ToString("yyyy-MM-dd"),
            HoraEntrada = r.Estado == "entrada" ? r.FechaEscaneo.ToString("HH:mm") : null,
            HoraSalida = r.Estado == "salida" ? r.FechaEscaneo.ToString("HH:mm") : null,
            Estado = r.Estado
        }).ToList();

        var json = JsonConvert.SerializeObject(exportables, Formatting.Indented);
        var nombreArchivo = $"asistencia_{DateTime.Now:yyyyMMdd_HHmm}.json";
        var ruta = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
        File.WriteAllText(ruta, json);

        await MarcarComoSincronizadosAsync(registros);

        // Compartir el archivo
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Compartir asistencia",
            File = new ShareFile(ruta)
        });

        return ruta;
    }

    public async Task<string> ExportarAsync()
    {
        var registros = await ObtenerPendientesAsync();

        if (registros.Count == 0)
            return null; // No hay registros, no se genera archivo


        var exportables = registros.Select(r => new RegistroExportado
        {
            UrlEscaneo = r.UrlEscaneo,
            Fecha = r.FechaEscaneo.ToString("yyyy-MM-dd"),
            HoraEntrada = r.Estado == "entrada" ? r.FechaEscaneo.ToString("HH:mm") : null,
            HoraSalida = r.Estado == "salida" ? r.FechaEscaneo.ToString("HH:mm") : null,
            Estado = r.Estado
        }).ToList();

        var json = JsonConvert.SerializeObject(exportables, Newtonsoft.Json.Formatting.Indented);
        var nombreArchivo = $"asistencia_{DateTime.Now:yyyyMMdd_HHmm}.json";
        var ruta = Path.Combine(FileSystem.AppDataDirectory, nombreArchivo);
        File.WriteAllText(ruta, json);

        await MarcarComoSincronizadosAsync(registros);
        return ruta;
    }

    private async Task<List<RegistroAsistencia>> ObtenerPendientesAsync()
    {
        await DB.InitAsync();
        return await DB.ObtenerPendientesAsync(); // registros con Sincronizado == false
    }

    private async Task MarcarComoSincronizadosAsync(List<RegistroAsistencia> registros)
    {
        foreach (var r in registros)
        {
            r.Sincronizado = true;
            r.EstadoSincronizacion = "Exportado";
            await DB.ActualizarAsync(r);
        }
    }
}
