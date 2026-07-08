namespace InvestView.Api.Hubs;

internal static class QuoteHubGroups
{
    public static string Symbol(string boardId, string symbol)
    {
        return $"quote:{Normalize(boardId)}:{Normalize(symbol)}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
