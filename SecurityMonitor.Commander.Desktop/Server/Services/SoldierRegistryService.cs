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
        private readonly string _dbPath;

        public SoldierRegistryService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbFolder = Path.Combine(appData, "SecurityMonitorCommander");
            Directory.CreateDirectory(dbFolder);
            _dbPath = Path.Combine(dbFolder, "Soldiers.db");
        }

        public void RegisterHeartbeat(HeartbeatDto heartbeat)
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<SoldierRecord>("soldiers");
            
            var existing = col.FindById(heartbeat.MachineName);
            if (existing == null)
            {
                existing = new SoldierRecord { Id = heartbeat.MachineName };
            }

            existing.MachineName = heartbeat.MachineName;
            existing.UserName = heartbeat.UserName;
            existing.IpAddress = heartbeat.IpAddress;
            existing.ConnectionType = heartbeat.ConnectionType;
            existing.CurrentNetworkSsid = heartbeat.CurrentNetworkSsid;
            existing.Latitude = heartbeat.Latitude;
            existing.Longitude = heartbeat.Longitude;
            existing.IsInAllowedRadius = heartbeat.IsInAllowedRadius;
            existing.IsOnAuthorizedNetwork = heartbeat.IsOnAuthorizedNetwork;
            existing.LastSeen = DateTime.UtcNow;

            col.Upsert(existing);
        }

        public List<SoldierRecord> GetAllSoldiers()
        {
            using var db = new LiteDatabase(_dbPath);
            return db.GetCollection<SoldierRecord>("soldiers").FindAll().ToList();
        }
    }
}
