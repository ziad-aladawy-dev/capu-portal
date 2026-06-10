using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Locally synced Treasury receipts (Portal Guid ids). Complements the live
/// passthrough on <c>api/treasury/receipts</c>: the live feed is keyed by
/// Treasury's integer ids, while fee rows and service-receipt mappings
/// reference these local Guid ids.
/// </summary>
[ApiController]
[Route("api/payments/receipts")]
public class PaymentReceiptsController : ControllerBase
{
    private readonly IReceiptQueryService _receipts;

    public PaymentReceiptsController(IReceiptQueryService receipts)
    {
        _receipts = receipts;
    }

    [HttpGet]
    [HasPermission(PermissionNames.PaymentTransactions.View)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _receipts.GetAllAsync(cancellationToken);
        return Ok(result);
    }
}
