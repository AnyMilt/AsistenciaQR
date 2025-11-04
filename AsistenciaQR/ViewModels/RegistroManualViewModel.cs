using AsistenciaQR.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Windows.Input;

namespace AsistenciaQR.ViewModels
{
    public class RegistroManualViewModel : BindableObject
    {
        private string _estadoMensaje;
        public string EstadoMensaje
        {
            get => _estadoMensaje;
            set
            {
                if (_estadoMensaje != value)
                {
                    _estadoMensaje = value;
                    OnPropertyChanged();
                }
            }
        }

        private readonly HttpClient _http = new HttpClient();

        public ObservableCollection<DocenteModel> Docentes { get; set; } = new();
        public ObservableCollection<DocenteModel> DocentesFiltrados { get; set; } = new();

        private string _filtroBusqueda;
        public string FiltroBusqueda
        {
            get => _filtroBusqueda;
            set
            {
                _filtroBusqueda = value;
                OnPropertyChanged();
                FiltrarDocentes();
            }
        }


        public ICommand RegistrarCommand { get; }

        public RegistroManualViewModel()
        {
            RegistrarCommand = new Command<DocenteModel>(RegistrarAsistencia);
            _ = CargarDocentes();
        }

        public async Task CargarDocentes()
        {
            try
            {
                string baseUrl = Preferences.Get("ServidorURL", "https://tu-servidor.com");
                baseUrl = baseUrl.Replace("/asistencia/registrar", "");
                string url = $"{baseUrl}/docentes/api/lista";

                var lista = await _http.GetFromJsonAsync<List<DocenteModel>>(url);
                if (lista != null)
                {
                    Docentes.Clear();
                    foreach (var d in lista)
                    {
                        // 🔥 Usa el valor que viene del backend
                        d.TipoAsistencia = d.TipoAsistencia ?? d.TipoAsistencia ?? "Entrada";
                        Docentes.Add(d);
                    }
                    FiltrarDocentes();
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeTemporal($"No se pudo cargar la lista: {ex.Message}");
            }
        }


        private void FiltrarDocentes()
        {
            var texto = _filtroBusqueda?.ToLower() ?? "";
            var filtrados = string.IsNullOrWhiteSpace(texto)
                ? Docentes
                : new ObservableCollection<DocenteModel>(
                    Docentes.Where(d =>
                        d.Nombre.ToLower().Contains(texto) ||
                        d.Cedula.ToLower().Contains(texto)));

            DocentesFiltrados.Clear();
            foreach (var d in filtrados)
                DocentesFiltrados.Add(d);
        }

        public async void RegistrarAsistencia(DocenteModel docente)
        {
            string baseUrl = Preferences.Get("ServidorURL", "https://tu-servidor.com/asistencia/registrar");

            // 🧹 Asegurar que termina correctamente
            if (!baseUrl.EndsWith("/asistencia/registrar"))
                baseUrl = $"{baseUrl.TrimEnd('/')}/asistencia/registrar";

            string docenteId = docente.Id.ToString();
            string tipo = docente.TipoAsistencia;
            string fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string deviceId = DeviceInfo.Current.Idiom.ToString();
            string lat = "0";
            string lng = "0";

            string fechaEncoded = Uri.EscapeDataString(fecha);
            string tipoEncoded = Uri.EscapeDataString(tipo);

            string urlFinal =
                $"{baseUrl}?docente={docenteId}&fecha={fechaEncoded}" +
                $"&tipo={tipoEncoded}&device_id={deviceId}&latitud={lat}&longitud={lng}";

            try
            {
                var response = await _http.GetAsync(urlFinal);

                if (response.IsSuccessStatusCode)
                {
                    // 🟢 Cambiar tipo antes de mostrar mensaje
                    docente.TipoAsistencia = tipo == "Entrada" ? "Salida" : "Entrada";

                    // 🔄 Recargar lista desde el servidor (actualiza el listado)
                    await CargarDocentes();

                    string mensaje = $"✅ Asistencia de {tipo} registrada correctamente para {docente.Nombre}";
                    await MostrarMensajeTemporal(mensaje);
                }
                else
                {
                    string detalle = await response.Content.ReadAsStringAsync();
                    await MostrarMensajeTemporal($"⚠️ No se pudo registrar la asistencia.\n{detalle}");
                }
            }
            catch (Exception ex)
            {
                await MostrarMensajeTemporal($"❌ Error: {ex.Message}");
            }
        }

        private async Task MostrarMensajeTemporal(string mensaje, int milisegundos = 3000)
        {
            EstadoMensaje = mensaje;
            await Task.Delay(milisegundos);
            EstadoMensaje = "Esperando escaneo...";
        }

    }
}
