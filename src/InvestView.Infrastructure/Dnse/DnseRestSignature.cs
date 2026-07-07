namespace InvestView.Infrastructure.Dnse;

public sealed record DnseRestSignature(
    string DateHeaderName,
    string DateHeaderValue,
    string SignatureHeaderValue,
    string? Nonce);
