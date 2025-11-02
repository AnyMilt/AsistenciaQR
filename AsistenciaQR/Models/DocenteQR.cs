using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsistenciaQR.Models
{
    public class DocenteQR
    {
        public string idDocente { get; set; }
        public string idDispositivo { get; set; } = string.Empty;
        public string lat { get; set; } = string.Empty;
        public string lng { get; set; } = string.Empty;
        public string tipo { get; set; } = string.Empty;
        public string fecha { get; set; } = string.Empty;
    }
}
