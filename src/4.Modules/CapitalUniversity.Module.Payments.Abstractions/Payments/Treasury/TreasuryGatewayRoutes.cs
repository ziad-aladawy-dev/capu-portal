namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>
/// Maps a <see cref="Gateway"/> to its HU Treasury route segments. NOTE: the
/// Treasury spec uses <c>eFinance</c> for initiate but <c>efinance</c> for
/// status — the casing genuinely differs, so initiate and status segments are
/// resolved separately.
/// </summary>
public static class TreasuryGatewayRoutes
{
    public static string InitiateSegment(Gateway gateway) => gateway switch
    {
        Gateway.Mastercard => "mastercard",
        Gateway.BankMisr => "bm",
        Gateway.EFinance => "eFinance",
        _ => throw new ArgumentOutOfRangeException(nameof(gateway), gateway, "Unsupported gateway."),
    };

    public static string StatusSegment(Gateway gateway) => gateway switch
    {
        Gateway.Mastercard => "mastercard",
        Gateway.BankMisr => "bm",
        Gateway.EFinance => "efinance",
        _ => throw new ArgumentOutOfRangeException(nameof(gateway), gateway, "Unsupported gateway."),
    };

    public static string InitiatePath(Gateway gateway) => $"api/payments/{InitiateSegment(gateway)}/initiate";

    public static string StatusPath(Gateway gateway, string merchantOrderId) =>
        $"api/payments/{StatusSegment(gateway)}/status/{merchantOrderId}";
}
