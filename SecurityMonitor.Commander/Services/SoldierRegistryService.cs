using LiteDB;
using SecurityMonitor.Commander.Models;
using SecurityMonitor.Shared.Models;
using System;
using System.Collections.Generic;

using System.IO;

namespace SecurityMonitor.Commander.Services
{
    public class SoldierRegistryService
    {
        private readonly string _dbPath = Path.Combine(AppContext.BaseDirectory, "Soldiers.db");

        public void RegisterHeartbeat(HeartbeatDto heartbeat)
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<SoldierRecord>("soldiers");
            
            // Usar HardwareFingerprint como ID único (basado en MAC de fábrica, no se puede falsificar)
            var soldierKey = !string.IsNullOrEmpty(heartbeat.HardwareFingerprint) 
                ? heartbeat.HardwareFingerprint 
                : heartbeat.MachineName;

            var existing = col.FindById(soldierKey);
            if (existing == null)
            {
                existing = new SoldierRecord { Id = soldierKey };
            }

            existing.MachineName = heartbeat.MachineName;
            existing.UserName = heartbeat.UserName;
            existing.MacAddress = heartbeat.MacAddress;
            existing.HardwareFingerprint = heartbeat.HardwareFingerprint;
            existing.IpAddress = heartbeat.IpAddress;
            existing.ConnectionType = heartbeat.ConnectionType;
            existing.CurrentNetworkSsid = heartbeat.CurrentNetworkSsid;
            existing.Latitude = heartbeat.Latitude;
            existing.Longitude = heartbeat.Longitude;
            existing.IsInAllowedRadius = heartbeat.IsInAllowedRadius;
            existing.IsOnAuthorizedNetwork = heartbeat.IsOnAuthorizedNetwork;
            existing.LastSeen = heartbeat.Timestamp;

            col.Upsert(existing);
        }

        public List<SoldierRecord> GetAllSoldiers()
        {
            using var db = new LiteDatabase(_dbPath);
            return db.GetCollection<SoldierRecord>("soldiers").FindAll().ToList();
        }
    }
}
