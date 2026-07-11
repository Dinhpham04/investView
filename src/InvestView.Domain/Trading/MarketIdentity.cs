namespace InvestView.Domain.Trading;

public static class MarketIdentity
{
    public static string NormalizeSymbol(string symbol)
    {
        return NormalizeRequired(symbol, nameof(symbol));
    }

    public static string NormalizeBoardId(string boardId)
    {
        return NormalizeRequired(boardId, nameof(boardId));
    }

    public static string NormalizeCurrency(string currency)
    {
        return NormalizeRequired(currency, nameof(currency));
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Value cannot contain whitespace.", parameterName);
        }

        return normalized;
    }
}
