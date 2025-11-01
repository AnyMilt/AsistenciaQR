using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsistenciaQR.Models
{
    public class RegistroExportado
    {
        public string UrlEscaneo { get; set; }
        public string Fecha { get; set; }         // yyyy-MM-dd
        public string HoraEntrada { get; set; }   // HH:mm
        public string HoraSalida { get; set; }    // HH:mm
        public string Estado { get; set; }

    }
}
