using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AsistenciaQR.Models
{
    public class DocenteModel
    {
        public int Id { get; set; }
        public string Cedula { get; set; }
        public string Nombre { get; set; }
        public string Jornada { get; set; }
        public string Tipo { get; set; }

        [JsonPropertyName("tipo_asistencia")]
        public string TipoAsistencia { get; set; } = "Entrada";

        // 🔹 Propiedad derivada para el color del botón
        public string ColorBoton => TipoAsistencia == "Entrada" ? "#28a745" : "#d9534f";

        public string NombreCompleto => $"{Cedula} - {Nombre}";
    }
}
