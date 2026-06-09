using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Admin/ops surface over the HU Treasury integration. Phase 2: live receipt
/// fetch (passthrough). Additive — does not touch existing payment endpoints.
/// </summary>
[ApiController]
[Route("api/treasury")]
public class TreasuryController : ControllerBase
{
    private readonly ITreasuryClient _treasury;

    public TreasuryController(ITreasuryClient treasury)
    {
        _treasury = treasury;
    }

    /// <summary>
    /// Live fetch of Treasury receipts (ConnectionTypeId = 6 only). Reuses the
    /// payment-transactions view permission until a dedicated receipts
    /// permission is introduced.
    /// </summary>
    [HttpGet("receipts")]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetReceipts(CancellationToken cancellationToken)
    {
        var receipts = await _treasury.GetReceiptsAsync(cancellationToken);
        return Ok(receipts);
    }
}
