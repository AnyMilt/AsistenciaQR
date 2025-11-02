using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AsistenciaQR.Services;
using AsistenciaQR.Models;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System.Text.Json;

namespace AsistenciaQR.ViewModels
{
    public partial class ScannerViewModel : ObservableObject
    {
        private bool procesandoCodigo = false;
        public bool ProcesandoCodigo => procesandoCodigo;

        private string? ultimoQr;
        private DateTime ultimoEscaneo;


        private readonly LocalStorageService storage;
        private readonly SyncService sync;


        [ObservableProperty]
        private string estadoMensaje = string.Empty;

        [ObservableProperty]
        private bool escaneoActivo = true;

        public ICommand ProcesarCodigoCommand { get; }
        public ICommand ReactivarEscaneoCommand { get; }
        public ICommand SincronizarCommand { get; }

        private bool EstaEnWifi() => Connectivity.ConnectionProfiles.Contains(ConnectionProfile.WiFi);

        private string _horaActual;
        public string HoraActual
        {
            get => _horaActual;
            set => SetProperty(ref _horaActual, value);
        }

        private Timer _timer;

        private string ObtenerDeviceId()
        {
            var deviceId = Preferences.Get("device_id", string.Empty);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Set("device_id", deviceId);
            }
            return deviceId;
        }

        private async Task<(double? lat, double? lng)> ObtenerUbicacionAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null)
                    return (location.Latitude, location.Longitude);
            }
            catch
            {
                // Si falla, devolvemos null
            }
            return (null, null);
        }

        public async Task SincronizarSiDisponibleAsync()
        {
            if (!EstaEnWifi())
            {
                await MostrarMensajeTemporal("⚠️ No estás conectado a Wi-Fi.");
                return;
            }

            if (!await ServidorDisponibleAsync())
            {
                await MostrarMensajeTemporal("⚠️ No se puede acceder al servidor institucional.");
                return;
            }

            await DB.InitAsync();
            var pendientes = await DB.ObtenerPendientesAsync();

            foreach (var registro in pendientes)
            {
                try
                {
                    using var httpClient = new HttpClient();

                    var fecha = registro.FechaEscaneo.ToString("yyyy-MM-dd");
                    var hora = registro.FechaEscaneo.ToString("HH:mm:ss");

                    var uriConFechaHora = $"{registro.UrlEscaneo}&fecha={fecha}&hora={hora}";
                    var respuesta = await httpClient.GetAsync(uriConFechaHora);

                    if (respuesta.IsSuccessStatusCode)
                        await DB.MarcarSincronizadoAsync(registro.Id);
                }
                catch
                {
                    // Error de red, se reintentará luego
                }
            }

            await MostrarMensajeTemporal("✅ Sincronización completada.");
        }
        private async Task<bool> ServidorDisponibleAsync()
        {
            try
            {
                var host = await DB.ObtenerHostUltimoRegistroAsync();
                if (string.IsNullOrWhiteSpace(host))
                    host = "asistencia.local"; // valor por defecto si no hay registros

                var urlPing = $"http://{host}:5000/ping";

                using var client = new HttpClient();
                var respuesta = await client.GetAsync(urlPing);
                return respuesta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }

        }



        public ScannerViewModel(LocalStorageService storageService, SyncService syncService)
        {
            storage = storageService;
            sync = syncService;

            ProcesarCodigoCommand = new AsyncRelayCommand<string>(ProcesarCodigoAsync);
            ReactivarEscaneoCommand = new RelayCommand(() => EscaneoActivo = true);
            SincronizarCommand = new AsyncRelayCommand(SincronizarSiDisponibleAsync);
            IniciarReloj();

        }

        private void IniciarReloj()
        {
            _timer = new Timer(_ =>
            {
                HoraActual = DateTime.Now.ToString("HH:mm:ss");
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }


        private async Task ProcesarCodigoAsync(string qrTexto)
        {
            if (procesandoCodigo || !EscaneoActivo)
                return;

            if (qrTexto == ultimoQr && (DateTime.Now - ultimoEscaneo).TotalSeconds < 5)
                return;

            procesandoCodigo = true;
            EscaneoActivo = false;
            ultimoQr = qrTexto;
            ultimoEscaneo = DateTime.Now;

            try
            {
                // 📦 Deserializar el JSON del QR
                DocenteQR? qrData = null;
                try
                {
                    qrData = JsonSerializer.Deserialize<DocenteQR>(qrTexto);
                }
                catch
                {
                    await MostrarMensajeTemporal("⚠️ Código QR inválido o con formato incorrecto.");
                    return;
                }

                if (qrData == null || Int32.Parse(qrData.idDocente) <= 0)
                {
                    await MostrarMensajeTemporal("⚠️ El código QR no contiene datos válidos del docente.");
                    return;
                }

                // 🧩 Aquí antes se calculaban valores — AHORA se usan los del JSON
                var baseUrl = "https://asistencia.instituto.edu/asistencia/registrar"; // URL fija o configurable
                var docenteId = qrData.idDocente;
                var deviceId = qrData.idDispositivo;
                var lat = qrData.lat;
                var lng = qrData.lng;
                var tipo = qrData.tipo;
                var fecha = qrData.fecha;

                // 🔗 Construcción del URL (igual que antes, pero usando datos del JSON)
                var urlSincronizacion = $"{baseUrl}?docente={docenteId}&fecha={fecha}" +
                                        $"&tipo={tipo}&device_id={deviceId}&latitud={lat}&longitud={lng}";

                Uri urifinal = new Uri(urlSincronizacion);

                // 🚀 Intento de registro en línea
                if (await IntentarRegistroEnLinea(urifinal))
                {
                    await MostrarMensajeTemporal($"✅ {tipo} registrada correctamente para docente {docenteId}.");
                    VibrarDispositivo();
                }
                else
                {
                    await MostrarMensajeTemporal("⚠️ Sin conexión. Se guardará localmente.");
                    await GuardarRegistroJsonLocal(qrData);
                }
            }
            finally
            {
                procesandoCodigo = false;
            }
        }

        private int? ObtenerDocenteIdDesdeQR(Uri uri)
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return int.TryParse(query["docente"], out int id) ? id : null;
        }

        private async Task<bool> IntentarRegistroEnLinea(Uri uri)
        {
            try
            {
                using var httpClient = new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var respuesta = await httpClient.GetAsync(uri, cts.Token);

                return respuesta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private void VibrarDispositivo()
        {
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
            }
            catch { /* Ignorar errores de vibración */ }
        }

        private async Task GuardarRegistroJsonLocal(DocenteQR qrData)
        {
            await DB.InitAsync();

            var urlSincronizacion = $"https://asistencia.instituto.edu/asistencia/registrar?" +
                                    $"docente={qrData.idDocente}&fecha={qrData.fecha}&tipo={qrData.tipo}" +
                                    $"&device_id={qrData.idDispositivo}&latitud={qrData.lat}&longitud={qrData.lng}";

            // Evita duplicados
            if (await DB.ExisteRegistroAsync(urlSincronizacion))
                return;

            var registro = new RegistroAsistencia
            {
                UrlEscaneo = urlSincronizacion,
                FechaEscaneo = DateTime.Now,
                Estado = "pendiente",
                Sincronizado = false,
                DeviceId = qrData.idDispositivo,
                Latitud = double.Parse(qrData.lat),
                Longitud = double.Parse(qrData.lng)
            };

            await DB.GuardarAsync(registro);
        }

        private async Task GuardarRegistroLocal(int docenteId, Uri uri, DateTime fechaEscaneo)
        {
            await DB.InitAsync();

            // Verifica si ya existen 2 escaneos pendientes para este docente
            var cantidad = await DB.ContarPendientesPorDocenteAsync(docenteId);
            if (cantidad >= 2)
                return;

            var baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/asistencia/registrar";
            var fecha = fechaEscaneo.ToString("yyyy-MM-dd");
            var hora = fechaEscaneo.ToString("HH:mm:ss");

            // Obtener datos del dispositivo y ubicación
            var deviceId = ObtenerDeviceId(); // método que genera/recupera un GUID persistente
            var (lat, lng) = await ObtenerUbicacionAsync(); // método que usa Geolocation.Default

            // Construir URL con todos los parámetros
            var urlSincronizacion = $"{baseUrl}?docente={docenteId}&fecha={fecha}&hora={hora}" +
                                    $"&device_id={deviceId}&latitud={lat}&longitud={lng}";

            // Verifica si ya existe un registro con la misma URL
            var yaExiste = await DB.ExisteRegistroAsync(urlSincronizacion);
            if (yaExiste)
                return;

            var registro = new RegistroAsistencia
            {
                UrlEscaneo = urlSincronizacion,
                FechaEscaneo = fechaEscaneo,
                Estado = "pendiente",
                Sincronizado = false,
                DeviceId = deviceId,
                Latitud = lat,
                Longitud = lng
            };

            await DB.GuardarAsync(registro);
        }

        private async Task MostrarMensajeTemporal(string mensaje, int milisegundos = 3000)
        {
            EstadoMensaje = mensaje;
            await Task.Delay(milisegundos);
            EstadoMensaje = "Esperando escaneo...";
        }


    }
}