using System.Text.Json;

namespace InvestView.Infrastructure.Dnse;

public interface IDnseMarketDataClient
{
    Task<JsonDocument> GetJsonAsync(
        string path,
        IReadOnlyDictionary<string, string?>? query,
        CancellationToken cancellationToken);
}
