using System.Security.Cryptography;
using System.Text;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;

namespace SecurityMonitor.Shared.Helpers;

[SupportedOSPlatform("windows")]
public static class CryptoService
{
    // Sal fija para fortalecer la derivación de claves (Cisco IKEv2 style)
    private static readonly byte[] StaticSalt = Encoding.UTF8.GetBytes("SM-Cisco-IKEv2-SecurityProtocol-v2");
    
    // Clave de tráfico maestra compartida entre Soldado y Comandante
    private static readonly byte[] TrafficSecret = Encoding.UTF8.GetBytes("SM-Cisco-IPsec-Heartbeat-Traffic-AES256-Key");

    // Prefijo para identificar valores encriptados en appsettings.json
    public const string EncryptedPrefix = "SECURE:";

    // Cache del fingerprint para no recalcular cada vez
    private static string? _cachedFingerprint;

    // ═══════════════════════════════════════════════════════════════
    //  HARDWARE IDENTITY (MAC Address = ID de Fábrica)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Obtiene la MAC Address principal del equipo (ID de fábrica, no cambia con reset).
    /// </summary>
    public static string GetPrimaryMacAddress()
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                         && !n.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                         && !n.Description.Contains("Pseudo", StringComparison.OrdinalIgnoreCase)
                         && !n.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.Speed)
                .FirstOrDefault();

            if (nic != null)
            {
                return nic.GetPhysicalAddress().ToString();
            }
        }
        catch { }

        return "00000000000000";
    }

    /// <summary>
    /// Genera un fingerprint único del equipo combinando MAC + MachineGuid + BIOS UUID.
    /// Este fingerprint NO cambia aunque se reinstale Windows o se resetee el equipo.
    /// </summary>
    public static string GetHardwareFingerprint()
    {
        if (_cachedFingerprint != null) return _cachedFingerprint;

        var mac = GetPrimaryMacAddress();
        var machineGuid = GetMachineGuid() ?? "NO-GUID";
        var biosSerial = GetBiosSerial() ?? "NO-BIOS";

        // Combinar las 3 fuentes de identidad
        string combined = $"MAC:{mac}|GUID:{machineGuid}|BIOS:{biosSerial}";

        // Generar un hash SHA-256 como fingerprint final
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
        _cachedFingerprint = Convert.ToHexString(hash);
        return _cachedFingerprint;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRÁFICO ENCRIPTADO (AES-256 + HMAC-SHA256)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Encripta datos para tráfico de red (clave compartida Comandante ↔ Soldado).
    /// </summary>
    public static string EncryptTraffic(string plainText)
    {
        return EncryptWithKey(plainText, GetTrafficKey());
    }

    /// <summary>
    /// Desencripta datos recibidos por red.
    /// </summary>
    public static string DecryptTraffic(string cipherText)
    {
        return DecryptWithKey(cipherText, GetTrafficKey());
    }

    /// <summary>
    /// Genera un HMAC-SHA256 para verificar la integridad del paquete.
    /// </summary>
    public static string GenerateHmac(string data)
    {
        byte[] key = GetTrafficKey();
        using var hmac = new HMACSHA256(key);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ALMACENAMIENTO LOCAL ENCRIPTADO (vinculado a hardware)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Encripta con clave derivada del hardware local (MAC + BIOS).
    /// </summary>
    public static string Encrypt(string plainText)
    {
        string encrypted = EncryptWithKey(plainText, GetHardwareDerivedKey());
        return EncryptedPrefix + encrypted;
    }

    /// <summary>
    /// Desencripta texto cifrado con clave de hardware. Si no está cifrado, devuelve el original.
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText) || !cipherText.StartsWith(EncryptedPrefix)) 
            return cipherText;

        string data = cipherText.Substring(EncryptedPrefix.Length);
        return DecryptWithKey(data, GetHardwareDerivedKey());
    }

    // ═══════════════════════════════════════════════════════════════
    //  MOTOR CRIPTOGRÁFICO INTERNO
    // ═══════════════════════════════════════════════════════════════

    private static string EncryptWithKey(string plainText, byte[] key)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    private static string DecryptWithKey(string base64Data, byte[] key)
    {
        if (string.IsNullOrEmpty(base64Data)) return base64Data;

        try
        {
            byte[] fullCipher = Convert.FromBase64String(base64Data);

            using var aes = Aes.Create();
            aes.Key = key;

            byte[] iv = new byte[aes.BlockSize / 8];
            byte[] cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
        catch
        {
            return "ERROR_DECRYPTION";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DERIVACIÓN DE CLAVES
    // ═══════════════════════════════════════════════════════════════

    private static byte[] GetTrafficKey()
    {
        using var rfc = new Rfc2898DeriveBytes(TrafficSecret, StaticSalt, 5000, HashAlgorithmName.SHA256);
        return rfc.GetBytes(32);
    }

    /// <summary>
    /// Clave derivada de la MAC Address (ID de fábrica) + BIOS Serial.
    /// Garantiza que el archivo solo funcione en ESTE equipo físico.
    /// </summary>
    private static byte[] GetHardwareDerivedKey()
    {
        string fingerprint = GetHardwareFingerprint();

        using var rfc = new Rfc2898DeriveBytes(fingerprint, StaticSalt, 10000, HashAlgorithmName.SHA256);
        return rfc.GetBytes(32); // AES-256
    }

    // ═══════════════════════════════════════════════════════════════
    //  PROVEEDORES DE IDENTIDAD DE HARDWARE
    // ═══════════════════════════════════════════════════════════════

    private static string? GetMachineGuid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch { return null; }
    }

    private static string? GetBiosSerial()
    {
        try
        {
            // Leer UUID de la BIOS vía WMI (grabado de fábrica, no se puede cambiar)
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString();
            }
        }
        catch { }
        return null;
    }
}

