using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.ServiceProcess;
using System.Diagnostics;
using System.Windows.Threading;
using System.Windows.Media;
using SecurityMonitor.Shared.Helpers;

namespace SecurityMonitor.Soldier.Config
{
    public partial class MainWindow : Window
    {
        private string _configPath = string.Empty;
        private DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            ResolveConfigPath();
            LoadConfig();
            
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.Tick += (s, e) => UpdateServiceStatus();
            _statusTimer.Start();
            UpdateServiceStatus();

            this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Busca appsettings.json en múltiples ubicaciones posibles.
        /// Prioridad: 1) Carpeta del servicio instalado, 2) Carpeta del exe, 3) Carpeta actual
        /// </summary>
        private void ResolveConfigPath()
        {
            // 1. Buscar en la carpeta del servicio Windows instalado
            try
            {
                using var sc = new ServiceController("SecurityMonitorSoldier");
                var imagePath = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SecurityMonitorSoldier")
                    ?.GetValue("ImagePath")?.ToString()?.Trim('"');
                
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var serviceDir = Path.GetDirectoryName(imagePath);
                    if (serviceDir != null)
                    {
                        var candidate = Path.Combine(serviceDir, "appsettings.json");
                        if (File.Exists(candidate))
                        {
                            _configPath = candidate;
                            return;
                        }
                    }
                }
            }
            catch { /* El servicio aún no está instalado */ }

            // 2. Buscar junto al ejecutable del Config
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var candidate = Path.Combine(exeDir, "appsettings.json");
                if (File.Exists(candidate))
                {
                    _configPath = candidate;
                    return;
                }
            }
            catch { }

            // 3. Fallback: directorio actual
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    return; // No hay config para cargar, es primera vez
                }

                var json = File.ReadAllText(_configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("AgentSettings", out var agentSettings))
                {
                    if (agentSettings.TryGetProperty("HeartbeatEndpointUrl", out var urlProp))
                    {
                        var rawUrl = urlProp.GetString() ?? "";
                        txtCommanderUrl.Text = CryptoService.Decrypt(rawUrl);
                    }

                    if (agentSettings.TryGetProperty("AuthorizedNetwork", out var ssidProp))
                    {
                        var rawSsid = ssidProp.GetString() ?? "";
                        txtAuthorizedSsid.Text = CryptoService.Decrypt(rawSsid);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "No se tienen permisos para leer la configuración.\nAsegúrese de ejecutar como Administrador.",
                    "Permisos Requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando configuración:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    MessageBox.Show("Archivo de configuración no encontrado en:\n" + _configPath,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var json = File.ReadAllText(_configPath);
                var options = new JsonWriterOptions { Indented = true };
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, options))
                {
                    using var doc = JsonDocument.Parse(json);
                    writer.WriteStartObject();
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Name == "AgentSettings")
                        {
                            writer.WriteStartObject("AgentSettings");
                            foreach (var innerProp in prop.Value.EnumerateObject())
                            {
                                if (innerProp.Name == "HeartbeatEndpointUrl")
                                    writer.WriteString("HeartbeatEndpointUrl", CryptoService.Encrypt(txtCommanderUrl.Text));
                                else if (innerProp.Name == "AuthorizedNetwork")
                                    writer.WriteString("AuthorizedNetwork", CryptoService.Encrypt(txtAuthorizedSsid.Text));
                                else
                                    innerProp.WriteTo(writer);
                            }
                            writer.WriteEndObject();
                        }
                        else
                        {
                            prop.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                }

                File.WriteAllBytes(_configPath, stream.ToArray());
                
                RestartService();
                
                MessageBox.Show("Configuración guardada y servicio reiniciado.",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Sin permisos para escribir la configuración.\nEjecute la aplicación como Administrador.",
                    "Permisos Requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error guardando configuración:\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ocultar en lugar de cerrar
            e.Cancel = true;
            this.Hide();
        }

        private void RestartService()
        {
            try
            {
                using var sc = new ServiceController("SecurityMonitorSoldier");
                
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
                
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
            catch (InvalidOperationException)
            {
                // El servicio no está instalado aún, intentar vía sc.exe
                try
                {
                    RunSc("stop SecurityMonitorSoldier");
                    RunSc("start SecurityMonitorSoldier");
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo reiniciar el servicio:\n{ex.Message}",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateServiceStatus()
        {
            try
            {
                using var sc = new ServiceController("SecurityMonitorSoldier");
                var status = sc.Status;
                
                txtServiceStatus.Text = $"Estado del Servicio: {status}";
                
                switch (status)
                {
                    case ServiceControllerStatus.Running:
                        statusDot.Fill = Brushes.LimeGreen;
                        break;
                    case ServiceControllerStatus.Stopped:
                        statusDot.Fill = Brushes.Red;
                        txtServiceStatus.Text += " (¡Atención!)";
                        break;
                    default:
                        statusDot.Fill = Brushes.Orange;
                        break;
                }
            }
            catch
            {
                txtServiceStatus.Text = "Servicio no instalado";
                statusDot.Fill = Brushes.Gray;
            }
        }

        private static void RunSc(string arguments)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit(5000);
        }
    }
}