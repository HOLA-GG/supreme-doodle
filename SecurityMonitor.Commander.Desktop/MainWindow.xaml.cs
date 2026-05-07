using System.Windows;
using SecurityMonitor.Commander.Desktop.Server;
using SecurityMonitor.Commander.Desktop.Models;
using SecurityMonitor.Shared.Models;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace SecurityMonitor.Commander.Desktop
{
    public partial class MainWindow : Window
    {
        private CommanderServerHost _serverHost;
        public ObservableCollection<AgentStatusModel> AgentsList { get; set; } = new ObservableCollection<AgentStatusModel>();
        private DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();
            _serverHost = new CommanderServerHost();
            dgLogs.ItemsSource = AgentsList;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (s, e) => UpdateAgentStatuses();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _serverHost.StartAsync();
                
                string userDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecurityMonitorCommander");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
                
                await webView.EnsureCoreWebView2Async(env);
                webView.Source = new Uri("http://localhost:5000");

                // Cargar config inicial en la UI
                txtAuthorizedSsid.Text = _serverHost.AuthorizedSsid;
                txtLatitude.Text = _serverHost.BaseLatitude.ToString();
                txtLongitude.Text = _serverHost.BaseLongitude.ToString();
                txtRadius.Text = _serverHost.AllowedRadius.ToString();
                chkMuteAlerts.IsChecked = _serverHost.MuteAlerts;

                StaticLog.OnHeartbeat += HandleHeartbeat;
                StaticLog.OnSystemMessage += LogMessage;

                _timer.Start();
                LogMessage("Aplicación iniciada correctamente.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error iniciando el servidor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                statusCircle.Fill = System.Windows.Media.Brushes.Red;
                statusText.Text = "Error en el servidor";
            }
        }

        private void HandleHeartbeat(HeartbeatDto heartbeat)
        {
            Dispatcher.Invoke(() =>
            {
                var existing = AgentsList.FirstOrDefault(a => a.MachineName == heartbeat.MachineName);
                if (existing == null)
                {
                    existing = new AgentStatusModel { MachineName = heartbeat.MachineName };
                    AgentsList.Add(existing);
                }

                existing.IpAddress = heartbeat.IpAddress;
                existing.ConnectionInfo = $"{heartbeat.ConnectionType} ({heartbeat.CurrentNetworkSsid})";
                existing.LastSeen = heartbeat.Timestamp;
                
                bool isAlert = !heartbeat.IsOnAuthorizedNetwork || !heartbeat.IsInAllowedRadius;
                existing.Status = isAlert ? "ALERTA" : "Protegido";
            });
        }

        private void LogMessage(string message)
        {
            // Opcional: mostrar mensajes de sistema en algún lado, o ignorarlos.
        }

        private void UpdateAgentStatuses()
        {
            var now = DateTime.UtcNow;
            foreach (var agent in AgentsList)
            {
                var diff = now - agent.LastSeen;
                if (diff.TotalMinutes > 30)
                    agent.Status = "PERDIDO (>30m)";
                else if (diff.TotalMinutes > 5 && agent.Status == "Protegido")
                    agent.Status = "Apagado/Sin Red";
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            webView.Reload();
            LogMessage("Dashboard refrescado manualmente.");
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            _serverHost.AuthorizedSsid = txtAuthorizedSsid.Text;
            if (double.TryParse(txtLatitude.Text, out double lat)) _serverHost.BaseLatitude = lat;
            if (double.TryParse(txtLongitude.Text, out double lon)) _serverHost.BaseLongitude = lon;
            if (double.TryParse(txtRadius.Text, out double rad)) _serverHost.AllowedRadius = rad;
            _serverHost.MuteAlerts = chkMuteAlerts.IsChecked ?? false;

            MessageBox.Show("Configuración guardada correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            webView.Reload();
        }

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            await _serverHost.StopAsync();
        }

        private void OpenWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }
    }
}