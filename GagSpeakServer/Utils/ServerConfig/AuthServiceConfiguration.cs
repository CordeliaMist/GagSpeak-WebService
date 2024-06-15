﻿using System.Text;

namespace GagspeakServer.Utils.Configuration;

public class AuthServiceConfiguration : GagspeakConfigBase
{
    public string GeoIPDbCityFile { get; set; } = string.Empty;
    public bool UseGeoIP { get; set; } = false;
    public int FailedAuthForTempBan { get; set; } = 5;
    public int TempBanDurationInMinutes { get; set; } = 5;
    public List<string> WhitelistedIps { get; set; } = new();

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine(base.ToString());
        sb.AppendLine($"{nameof(RedisPool)} => {RedisPool}");
        sb.AppendLine($"{nameof(GeoIPDbCityFile)} => {GeoIPDbCityFile}");
        sb.AppendLine($"{nameof(UseGeoIP)} => {UseGeoIP}");
        return sb.ToString();
    }
}
