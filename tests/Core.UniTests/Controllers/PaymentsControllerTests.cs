using CapitalUniversity.API.Controllers;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Controllers;

public class PaymentsControllerTests
{
    private static PaymentsController NewController(out Mock<IPaymentVerificationService> svc)
    {
        svc = new Mock<IPaymentVerificationService>(MockBehavior.Strict);
        return new PaymentsController(svc.Object);
    }

    [Fact]
    public async Task Record_DelegatesAndReturnsOkWithBody()
    {
        var ctrl = NewController(out var svc);
        var req = new RecordPaymentRequest();
        var response = new PaymentTransactionResponse();
        svc.Setup(s => s.RecordAsync(req, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await ctrl.Record(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task GetForInvoice_DelegatesAndReturnsOk()
    {
        var ctrl = NewController(out var svc);
        var invoiceId = Guid.NewGuid();
        var list = (IReadOnlyList<PaymentTransactionResponse>)new List<PaymentTransactionResponse> { new() };
        svc.Setup(s => s.GetForInvoiceAsync(invoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var result = await ctrl.GetForInvoice(invoiceId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }
}
