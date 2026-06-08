namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Maps a <see cref="Gateway"/> to its HU Treasury route segment. Per the
/// Treasury API contract the segments are <c>mastercard</c>, <c>bm</c>, and
/// <c>efinance</c> (lowercase) for both initiate and status.
/// </summary>
public static class TreasuryGatewayRoutes
{
    public static string Segment(Gateway gateway) => gateway switch
    {
        Gateway.Mastercard => "mastercard",
        Gateway.BankMisr => "bm",
        Gateway.EFinance => "efinance",
        _ => throw new ArgumentOutOfRangeException(nameof(gateway), gateway, "Unsupported gateway."),
    };

    public static string InitiatePath(Gateway gateway) => $"api/payments/{Segment(gateway)}/initiate";

    public static string StatusPath(Gateway gateway, string merchantOrderId) =>
        $"api/payments/{Segment(gateway)}/status/{merchantOrderId}";
}
