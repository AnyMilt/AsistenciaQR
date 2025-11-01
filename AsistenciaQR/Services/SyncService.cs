using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AsistenciaQR.Models;
using SQLite;

namespace AsistenciaQR.Services
{
    public class SyncService
    {
        private readonly SQLiteAsyncConnection db;
        private readonly HttpClient http;

        public SyncService(SQLiteAsyncConnection database)
        {
            db = database;
            http = new HttpClient();
            http.BaseAddress = new Uri("https://tuservidor.com/api/"); // 🔧 Ajusta tu URL
        }

        public async Task SincronizarPendientesAsync()
        {
            var pendientes = await db.Table<RegistroAsistencia>()
                .Where(r => !r.Sincronizado).ToListAsync();

            foreach (var registro in pendientes)
            {
                try
                {
                    var response = await http.GetAsync(registro.UrlEscaneo); // ✅ Usamos la URL original

                    if (response.IsSuccessStatusCode)
                    {
                        registro.Sincronizado = true;
                        registro.EstadoSincronizacion = "Enviado";
                    }
                    else
                    {
                        registro.EstadoSincronizacion = $"Error: {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    registro.EstadoSincronizacion = $"Excepción: {ex.Message}";
                }

                await db.UpdateAsync(registro); // ✅ Se actualiza siempre, con éxito o error
            }
        }
    }
}