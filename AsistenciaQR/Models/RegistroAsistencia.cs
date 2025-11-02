using SQLite;

namespace AsistenciaQR.Models
{
    public class RegistroAsistencia
    {
        public RegistroAsistencia() { }

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string UrlEscaneo { get; set; }
        public DateTime FechaEscaneo { get; set; }
        public string Estado { get; set; } = "pendiente";
        public bool Sincronizado { get; set; } = false;
        public string EstadoSincronizacion { get; set; } = "Pendiente";

        // Nuevos campos para trazabilidad
        public string DeviceId { get; set; }   // identificador único del móvil
        public double? Latitud { get; set; }   // ubicación al momento del escaneo
        public double? Longitud { get; set; }
        public string tipo { get; set; } = string.Empty;

        [Ignore]
        public string FechaFormateada => FechaEscaneo.ToString("dd/MM/yyyy HH:mm");
    }
}