using InvestView.Application.Dtos.MarketData;

namespace InvestView.Infrastructure.Dnse;

public sealed record DnseWebSocketMessage(
    DnseWebSocketMessageKind Kind,
    MarketQuoteUpdateDto? QuoteUpdate = null,
    MarketTradeUpdateDto? TradeUpdate = null,
    string? ErrorMessage = null,
    string? Action = null);

public enum DnseWebSocketMessageKind
{
    Unknown,
    Ping,
    Pong,
    AuthSuccess,
    Subscribed,
    Error,
    QuoteUpdate,
    TradeUpdate
}
