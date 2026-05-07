namespace SecurityMonitorAgent.Services.Interfaces;

public interface IGeoLocationService
{
    Task<(double Latitude, double Longitude)> GetCurrentLocationAsync();
    bool IsOutsideGeofence(double currentLat, double currentLon, double originLat, double originLon, double allowedRadiusMeters, bool isOnAuthorizedNetwork);
}
