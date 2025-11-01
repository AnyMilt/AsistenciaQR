using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SQLite;
using AsistenciaQR.Models;

namespace AsistenciaQR.Services
{
    public class LocalStorageService
    {
        private SQLiteAsyncConnection db;

        public async Task InitAsync()
        {
            if (db != null) return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "asistencia.db3");
            db = new SQLiteAsyncConnection(dbPath);
            await db.CreateTableAsync<RegistroAsistencia>();
        }

        public async Task GuardarRegistroAsync(RegistroAsistencia registro)
        {
            await InitAsync();

            registro.Sincronizado = false;
            registro.EstadoSincronizacion = "Pendiente";
            registro.FechaEscaneo = DateTime.Now;

            await db.InsertAsync(registro);
        }

        public async Task<List<RegistroAsistencia>> ObtenerPendientesAsync()
        {
            await InitAsync();
            return await db.Table<RegistroAsistencia>()
                .Where(r => !r.Sincronizado)
                .ToListAsync();
        }

        public async Task ActualizarRegistroAsync(RegistroAsistencia registro)
        {
            await InitAsync();
            await db.UpdateAsync(registro);
        }

        public async Task<List<RegistroAsistencia>> ObtenerTodosAsync()
        {
            await InitAsync();
            return await db.Table<RegistroAsistencia>().OrderByDescending(r => r.FechaEscaneo).ToListAsync();
        }

        public async Task EliminarRegistroAsync(int id)
        {
            await InitAsync();
            await db.DeleteAsync<RegistroAsistencia>(id);
        }
    }
}