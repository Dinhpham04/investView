using InvestView.Infrastructure.Dnse;
using Microsoft.Extensions.Options;

namespace InvestView.Infrastructure.Realtime;

public sealed class SecurityDefinitionWarmupSymbolResolver
{
    private readonly IDnseMarketDataClient _client;
    private readonly IOptions<SecurityDefinitionWarmupOptions> _options;

    public SecurityDefinitionWarmupSymbolResolver(
        IDnseMarketDataClient client,
        IOptions<SecurityDefinitionWarmupOptions> options)
    {
        _client = client;
        _options = options;
    }

    public async Task<SecurityDefinitionWarmupSymbolResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var pageSize = Math.Max(options.InstrumentPageSize, 1);
        var maxPages = Math.Max(options.MaxInstrumentPages, 1);
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var symbolsByMarket = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var marketId in NormalizeTokens(options.MarketIds))
        {
            var marketSymbols = new HashSet<string>(StringComparer.Ordinal);

            for (var page = 1; page <= maxPages; page++)
            {
                using var instruments = await _client.GetJsonAsync(
                    "/instruments",
                    new Dictionary<string, string?>
                    {
                        ["marketId"] = marketId,
                        ["securityGroupId"] = NormalizeToken(options.SecurityGroupId, "ST"),
                        ["limit"] = pageSize.ToString(),
                        ["page"] = page.ToString()
                    },
                    cancellationToken);

                var pageSymbols = DnseMarketDataMapper.ExtractInstrumentSymbols(instruments.RootElement, pageSize);
                foreach (var symbol in NormalizeTokens(pageSymbols))
                {
                    marketSymbols.Add(symbol);
                    symbols.Add(symbol);
                }

                if (pageSymbols.Count < pageSize)
                {
                    break;
                }
            }

            symbolsByMarket[marketId] = marketSymbols.Order(StringComparer.Ordinal).ToArray();
        }

        return new SecurityDefinitionWarmupSymbolResolution(
            symbols.Order(StringComparer.Ordinal).ToArray(),
            symbolsByMarket);
    }

    private static IReadOnlyList<string> NormalizeTokens(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => NormalizeToken(value, string.Empty))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeToken(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }
}

public sealed record SecurityDefinitionWarmupSymbolResolution(
    IReadOnlyList<string> Symbols,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SymbolsByMarket);
