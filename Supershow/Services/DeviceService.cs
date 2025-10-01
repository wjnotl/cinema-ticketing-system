using System.Text.Json;

namespace Supershow.Services;

public class DeviceService
{
    private readonly DB db;
    private readonly IHttpContextAccessor ct;
    private readonly VerificationService vs;
    private readonly IConfiguration cf;

    public DeviceService(DB db, IHttpContextAccessor ct, VerificationService vs, IConfiguration cf)
    {
        this.db = db;
        this.ct = ct;
        this.vs = vs;
        this.cf = cf;
    }

    public async Task<DeviceInfo> GetCurrentDeviceInfo()
    {
        var info = new DeviceInfo();

        string ip = ct.HttpContext!.Connection.RemoteIpAddress!.ToString();
        if (ct.HttpContext!.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            ip = ct.HttpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0];
        }

        if (ip == "::1" || ip == "127.0.0.1")
        {
            info.Location = "Local Host";
        }
        else
        {
            try
            {
                // Get Geo Info
                using var client = new HttpClient();
                var geoApi = $"https://api.findip.net/{ip}/?token={cf["IpNet:Token"]}";
                var response = await client.GetStringAsync(geoApi);
                var json = JsonDocument.Parse(response);
                var root = json.RootElement;

                string city = root.GetProperty("city").GetProperty("names").GetProperty("en").GetString() ?? "Unknown City";
                string region = root.GetProperty("subdivisions")[0].GetProperty("names").GetProperty("en").GetString() ?? "Unknown Region";
                string country = root.GetProperty("country").GetProperty("names").GetProperty("en").GetString() ?? "Unknown Country";

                info.Location = $"{city}, {region}, {country}".Trim(' ', ',');
            }
            catch
            {
                info.Location = "Local Host";
            }
        }


        string userAgent = ct.HttpContext!.Request.Headers.UserAgent.ToString();

        // Browser
        if (userAgent.Contains("Edg"))
            info.Browser = "Microsoft Edge";
        else if (userAgent.Contains("OPR") || userAgent.Contains("Opera"))
            info.Browser = "Opera";
        else if (userAgent.Contains("Chrome"))
            info.Browser = "Google Chrome";
        else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome"))
            info.Browser = "Safari";
        else if (userAgent.Contains("Firefox"))
            info.Browser = "Mozilla Firefox";
        else if (userAgent.Contains("MSIE") || userAgent.Contains("Trident"))
            info.Browser = "Internet Explorer";

        // OS
        if (userAgent.Contains("Windows"))
            info.OS = "Windows";
        else if (userAgent.Contains("Macintosh"))
            info.OS = "MacOS";
        else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            info.OS = "iOS";
        else if (userAgent.Contains("Android"))
            info.OS = "Android";
        else if (userAgent.Contains("Linux"))
            info.OS = "Linux";

        // Device
        if (
            userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
        )
            info.Type = "phone";
        else if (
            userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
        )
            info.Type = "tablet";


        return info;
    }

    public Device? GetKnownDeviceForAccount(int accountId, DeviceInfo deviceInfo)
    {
        return db.Devices.FirstOrDefault(
            u => u.AccountId == accountId
            && u.DeviceOS == deviceInfo.OS
            && u.DeviceType == deviceInfo.Type
            && u.DeviceBrowser == deviceInfo.Browser
        );
    }

    public async Task<(int, string)> CreateDevice(Account account, string baseUrl, bool verified = false)
    {
        var deviceInfo = await GetCurrentDeviceInfo();


        // Add new device
        Device device = new()
        {
            IsVerified = verified,
            Address = deviceInfo.Location,
            DeviceOS = deviceInfo.OS,
            DeviceType = deviceInfo.Type,
            DeviceBrowser = deviceInfo.Browser,
            AccountId = account.Id
        };
        db.Devices.Add(device);
        db.SaveChanges();

        // Add new verification
        var token = "";
        if (!verified)
        {
            var verification = vs.CreateVerification("Login", baseUrl, account.Id, device.Id);
            token = verification.Token;
        }

        return (device.Id, token);
    }
}

public class DeviceInfo
{
    public string Location { get; set; } = "Unknown";
    public string Browser { get; set; } = "Unknown Browser";
    public string OS { get; set; } = "Unknown OS";
    public string Type { get; set; } = "computer";
}