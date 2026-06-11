using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.Events;
using CapitalUniversity.Modules.Payments.Application.Treasury;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Payments;

/// <summary>
/// B7 — guards the drain handler that prevents every fee settlement from
/// poisoning the outbox. Asserts the handler claims the correct MessageType
/// (so the dispatcher routes "payments.fee.paid" to it instead of dead-lettering)
/// and that it deserialises the published <see cref="FeePaidFact"/> shape.
/// </summary>
public class FeePaidEventHandlerTests
{
    private static FeePaidEventHandler Build() =>
        new(NullLogger<FeePaidEventHandler>.Instance);

    [Fact]
    public void MessageType_MatchesFeePaidEventTypeKey()
    {
        Build().MessageType.Should().Be(FeePaidEvent.TypeKey);
        FeePaidEvent.TypeKey.Should().Be("payments.fee.paid");
    }

    [Fact]
    public async Task HandleAsync_DrainsPublishedFact_WithoutThrowing()
    {
        // Serialise exactly as SettlementService enqueues it.
        var fact = new FeePaidFact(
            FeeId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            StudentId: Guid.NewGuid(),
            SourceModule: "StudentServices",
            SourceReferenceId: Guid.NewGuid(),
            Amount: 250.00m,
            PaidAt: new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));
        var payload = JsonSerializer.Serialize(fact);

        var act = async () => await Build().HandleAsync(Guid.NewGuid(), payload, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_NullPayload_Throws()
    {
        var act = async () => await Build().HandleAsync(Guid.NewGuid(), "null", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
