using SQLite;

namespace AsistenciaQR.Models;

public static class DB
{
    public static SQLiteAsyncConnection Conexion { get; private set; }

    public static async Task InitAsync()
    {
        if (Conexion != null) return;

        var ruta = Path.Combine(FileSystem.AppDataDirectory, "asistencias.db");
        Conexion = new SQLiteAsyncConnection(ruta);
        await Conexion.CreateTableAsync<RegistroAsistencia>();
    }

    public static Task<int> GuardarAsync(RegistroAsistencia registro) =>
        Conexion.InsertAsync(registro);

    public static Task<List<RegistroAsistencia>> ObtenerPendientesAsync() =>
        Conexion.Table<RegistroAsistencia>().Where(r => !r.Sincronizado).ToListAsync();

    public static Task<int> MarcarSincronizadoAsync(int id) =>
     Conexion.ExecuteAsync("UPDATE RegistroAsistencia SET Sincronizado = 1, Estado = 'registrado' WHERE Id = ?", id);

    public static async Task<List<RegistroAsistencia>> ObtenerTodosAsync()
    {
        await InitAsync(); // Asegura que la base esté inicializada
        return await Conexion.Table<RegistroAsistencia>().ToListAsync();
    }

    public static async Task EliminarAsync(int id)
    {
        await InitAsync();
        await Conexion.DeleteAsync<RegistroAsistencia>(id);
    }

    public static async Task EliminarTodosAsync()
    {
        await InitAsync();
        await Conexion.DeleteAllAsync<RegistroAsistencia>();
    }
    public static async Task<bool> ExisteRegistroAsync(string urlEscaneo)
    {
        await InitAsync();
        var resultado = await Conexion.Table<RegistroAsistencia>()
                                .Where(r => r.UrlEscaneo == urlEscaneo)
                                .FirstOrDefaultAsync();
        return resultado != null;
    }

    public static async Task<string?> ObtenerHostUltimoRegistroAsync()
    {
        await InitAsync();
        var ultimo = await Conexion.Table<RegistroAsistencia>()
                             .OrderByDescending(r => r.FechaEscaneo)
                             .FirstOrDefaultAsync();

        if (ultimo == null || string.IsNullOrWhiteSpace(ultimo.UrlEscaneo))
            return null;

        if (Uri.TryCreate(ultimo.UrlEscaneo, UriKind.Absolute, out var uri))
            return uri.Host;

        return null;
    }

    public static async Task<int> ContarPendientesPorDocenteAsync(int docenteId)
    {
        await InitAsync();
        return await Conexion.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM RegistroAsistencia WHERE Sincronizado = 0 AND UrlEscaneo LIKE ?", $"%docente={docenteId}%"
        );
    }

    public static async Task ActualizarAsync(RegistroAsistencia registro)
    {
        
        await Conexion.UpdateAsync(registro);
    }

}
