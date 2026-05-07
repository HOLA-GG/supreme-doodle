using Windows.Devices.Geolocation;
using SecurityMonitorAgent.Services.Interfaces;

namespace SecurityMonitorAgent.Services.Implementations;

public class GeoLocationService : IGeoLocationService
{
    public async Task<(double Latitude, double Longitude)> GetCurrentLocationAsync()
    {
        // 1. Intentar con GPS Nativo de Windows
        try
        {
            var geolocator = new Geolocator { DesiredAccuracyInMeters = 50 };
            var position = await geolocator.GetGeopositionAsync(maximumAge: TimeSpan.FromMinutes(5), timeout: TimeSpan.FromSeconds(5));
            return (position.Coordinate.Point.Position.Latitude, position.Coordinate.Point.Position.Longitude);
        }
        catch
        {
            // 2. Fallback: Triangulación por IP usando un servicio más preciso (ipinfo.io o ipwhois)
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SecurityMonitor/1.0");
                var response = await client.GetStringAsync("https://ipwhois.app/json/");
                using var doc = System.Text.Json.JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (root.GetProperty("success").GetBoolean() == true)
                {
                    double lat = root.GetProperty("lat").GetDouble();
                    double lon = root.GetProperty("lon").GetDouble();
                    return (lat, lon);
                }
            }
            catch { }
            
            return (0, 0);
        }
    }

    public bool IsOutsideGeofence(double currentLat, double currentLon, double originLat, double originLon, double allowedRadiusMeters, bool isOnAuthorizedNetwork)
    {
        // Regla de Oro: Si está conectado al WiFi oficial de la empresa, automáticamente se considera "Dentro del Área", ignorando las coordenadas de IP fallidas.
        if (isOnAuthorizedNetwork) return false;

        if (currentLat == 0 && currentLon == 0) return true; // Si no hay ubicación y no está en la red, asumir fuera o en alerta.

        var distance = CalculateDistance(currentLat, currentLon, originLat, originLon);
        return distance > allowedRadiusMeters;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var d1 = lat1 * (Math.PI / 180.0);
        var num1 = lon1 * (Math.PI / 180.0);
        var d2 = lat2 * (Math.PI / 180.0);
        var num2 = lon2 * (Math.PI / 180.0) - num1;
        var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);
        
        return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3))); // Radio de la tierra en metros
    }
}
