namespace InvestView.Infrastructure.Dnse;

public sealed class DnseMarketDataOptions
{
    public const string SectionName = "Dnse";
    public const string DefaultBaseUrl = "https://openapi.dnse.com.vn";
    public const string DefaultApiVersion = "2026-05-07";
    public const string DefaultDateHeaderName = "Date";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public string ApiVersion { get; set; } = DefaultApiVersion;

    public string DateHeaderName { get; set; } = DefaultDateHeaderName;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public string Algorithm { get; set; } = "hmac-sha256";

    public bool HmacNonceEnabled { get; set; } = true;

    public string[] DefaultSymbols { get; set; } = ["HPG", "SSI", "VCB"];

    public int QuantityScaleFactor { get; set; } = 10;

    public int InstrumentPageSize { get; set; } = 100;

    public int MaxInstrumentPages { get; set; } = 20;

    public int ForeignTradingLookbackHours { get; set; } = 8;

    public bool LogResponseBodies { get; set; }

    public int MaxLoggedResponseBodyChars { get; set; } = 4000;

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}
