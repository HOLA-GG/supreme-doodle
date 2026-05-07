namespace SecurityMonitorAgent.Services.Interfaces;

public interface INetworkMonitorService
{
    bool IsInAuthorizedNetwork(string authorizedSsidOrIp);
    string GetMacAddress();
    string GetLocalIpAddress();
    string GetCurrentSsid();
    string GetActiveConnectionType();
    event EventHandler? NetworkChanged;
}
