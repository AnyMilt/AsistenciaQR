using AsistenciaQR.Models;
using AsistenciaQR.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

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


                // 🕒 Validación de fecha y tipo (simple y efectiva)
                if (!DateTime.TryParse(qrData.fecha, out var fechaQr))
                {
                    await MostrarMensajeTemporal("⚠️ Fecha del QR inválida.");
                    return;
                }

                var ahora = DateTime.Now;

                // Validar que el QR sea del mismo día
                if (fechaQr.Date != ahora.Date)
                {
                    await MostrarMensajeTemporal("⚠️ El QR pertenece a otro día.");
                    return;
                }

                // Obtener minutos configurados
                int minutosValidez = 10; // valor por defecto
                if (int.TryParse(Preferences.Get("MinutosQRValidez", "10"), out int m))
                    minutosValidez = m;

                // Si es entrada, validar que no tenga más de 10 minutos de diferencia
                if (qrData.tipo?.Equals("Entrada", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var diferencia = Math.Abs((ahora - fechaQr).TotalMinutes);

                    if (diferencia > minutosValidez)
                    {
                        await MostrarMensajeTemporal($"⚠️ QR de entrada vencido (más de {diferencia} minutos).");
                        return;
                    }
                }
                else if (!qrData.tipo?.Equals("Salida", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await MostrarMensajeTemporal("⚠️ Tipo de QR no reconocido (debe ser Entrada o Salida).");
                    return;
                }




                // 🧩 Datos del JSON
               
                // Recuperar desde las preferencias del usuario
                var baseUrl = Preferences.Get("ServidorURL", "https://invincibly-peachy-tyrone.ngrok-free.dev/asistencia/registrar");
                var docenteId = qrData.idDocente ?? "";
                var deviceId = qrData.idDispositivo ?? "";
                var lat = qrData.lat ?? "0";  // Si viene nulo, usa "0"
                var lng = qrData.lng ?? "0";  // Si viene nulo, usa "0"
                var tipo = qrData.tipo ?? "Entrada";
                var fecha = fechaQr.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                // ⚙️ Codificar parámetros para evitar errores en espacios o caracteres especiales
                string fechaEncoded = Uri.EscapeDataString(fecha);
                string tipoEncoded = Uri.EscapeDataString(tipo);
                string deviceIdEncoded = Uri.EscapeDataString(deviceId);

                // 🔗 Construcción del URL final
                string urlSincronizacion =
                    $"{baseUrl}?docente={docenteId}&fecha={fechaEncoded}" +
                    $"&tipo={tipoEncoded}&device_id={deviceIdEncoded}&latitud={lat}&longitud={lng}";

                Uri? urifinal = null;
                try
                {
                    urifinal = new Uri(urlSincronizacion);
                    Console.WriteLine($"✅ URL generada correctamente: {urifinal}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error generando URI: {ex.Message}");
                }


                // 🚀 Intento de registro en línea
                if (await IntentarRegistroEnLinea(urifinal))
                {
                    await MostrarMensajeTemporal($"✅ {tipo} registrada correctamente para docente {docenteId}.");
                    VibrarDispositivo();
                }
                else
                {
                    await MostrarMensajeTemporal("⚠️ Sin conexión. Se guardará localmente.");
                    await GuardarRegistroJsonLocal(qrData, urifinal);
                }
            }
            finally
            {
                procesandoCodigo = false;
            }
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

        private async Task GuardarRegistroJsonLocal(DocenteQR qrData, Uri urifinal)
        {
            await DB.InitAsync();



            // Evita duplicados
            if (await DB.ExisteRegistroAsync(urifinal.AbsoluteUri))
                return;

            var registro = new RegistroAsistencia
            {
                UrlEscaneo = urifinal.AbsoluteUri,
                FechaEscaneo = DateTime.Now,
                Estado = "pendiente",
                Sincronizado = false,
                DeviceId = qrData.idDispositivo ?? "",
                Latitud = double.Parse(qrData.lat ?? "0"),
                Longitud = double.Parse(qrData.lng ?? "0"),

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