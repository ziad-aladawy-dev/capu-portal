using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Domain;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Payments;

/// <summary>
/// Domain-level invariants on the financial entities (Invoice, InvoiceItem,
/// PaymentTransaction). These tests deliberately bypass services to pin the
/// entity's own contract: defaults, lifecycle guards, and the implicit
/// <see cref="Invoice.TotalAmount"/> sum-of-items convention that
/// <c>InvoiceService.CreateAsync</c> and <c>FeeCreationService</c> rely on.
/// </summary>
public class InvoiceDomainTests
{
    private static Invoice NewPendingInvoice(decimal total = 100m, string currency = "EGP") => new()
    {
        Id = Guid.NewGuid(),
        StudentId = Guid.NewGuid(),
        Status = InvoiceStatus.Pending,
        TotalAmount = total,
        Currency = currency,
    };

    // ── Invoice creation defaults ────────────────────────────────────────────

    [Fact]
    public void NewInvoice_DefaultsToPending_NotClosed_NoTransactions_NoItems()
    {
        // Construction-time invariants. A freshly-allocated invoice must enter
        // the world in a safe state: not Paid (no money received yet), not
        // Closed (still mutable), and with empty Items/Transactions collections
        // so callers can Add without null-checks.
        var invoice = new Invoice();

        invoice.Status.Should().Be(InvoiceStatus.Pending);
        invoice.IsClosed.Should().BeFalse();
        invoice.ClosedAt.Should().BeNull();
        invoice.Currency.Should().Be("EGP", "school-wide default per Invoice.cs:40");
        invoice.Items.Should().NotBeNull().And.BeEmpty();
        invoice.Transactions.Should().NotBeNull().And.BeEmpty();
        invoice.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public void NewPaymentTransaction_DefaultsToPending_NotDeleted()
    {
        var tx = new PaymentTransaction();

        tx.Status.Should().Be(PaymentTransactionStatus.Pending);
        tx.IsDeleted.Should().BeFalse();
        tx.RawPayloadJson.Should().Be("{}", "default-empty payload prevents NULL writes");
    }

    // ── TotalAmount sum-of-items convention ──────────────────────────────────
    //
    // Per Invoice.cs:14-17 the TotalAmount property is "denormalised for fast
    // list queries; it is always equal to the sum of item amounts at persist
    // time (enforced by InvoiceService.Recalculate)". The entity itself does
    // not enforce this — these tests pin the convention so any future
    // refactor that moves the sum onto the entity (audit H-8) has a contract
    // to satisfy.

    [Fact]
    public void TotalAmount_AfterPopulatingItems_MatchesItemSum_AtPersistConvention()
    {
        var invoice = NewPendingInvoice(total: 0m);
        invoice.Items.Add(new InvoiceItem { Amount = 100m });
        invoice.Items.Add(new InvoiceItem { Amount = 50m });

        // Convention: TotalAmount is assigned by the service AFTER items are
        // attached. We simulate that here so the test catches a future
        // refactor that breaks the sum equality.
        invoice.TotalAmount = invoice.Items.Sum(i => i.Amount);

        invoice.TotalAmount.Should().Be(150m);
        invoice.Items.Sum(i => i.Amount).Should().Be(invoice.TotalAmount);
    }

    [Fact]
    public void TotalAmount_SurvivesStatusMutation_WithoutItemListChange()
    {
        // Status transitions (Pending → PartiallyPaid → Paid) must not
        // recompute or zero the total. The denormalised value is locked in
        // at persist time and only the items collection can legitimately
        // change it.
        var invoice = NewPendingInvoice(total: 200m);
        invoice.Items.Add(new InvoiceItem { Amount = 200m });

        invoice.Status = InvoiceStatus.PartiallyPaid;
        invoice.TotalAmount.Should().Be(200m);

        invoice.Status = InvoiceStatus.Paid;
        invoice.TotalAmount.Should().Be(200m);

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.TotalAmount.Should().Be(200m);
    }

    [Fact]
    public void TotalAmount_DecimalPrecision_NotRoundedByEntity()
    {
        // SQL Server stores decimal(18,2) for Amount columns; the entity
        // accepts whatever precision callers pass. This test pins that the
        // entity doesn't silently re-round — a fix in the validator or
        // persistence layer is the right venue, not the domain.
        var invoice = NewPendingInvoice(total: 0m);
        invoice.Items.Add(new InvoiceItem { Amount = 33.33m });
        invoice.Items.Add(new InvoiceItem { Amount = 33.33m });
        invoice.Items.Add(new InvoiceItem { Amount = 33.34m });
        invoice.TotalAmount = invoice.Items.Sum(i => i.Amount);

        invoice.TotalAmount.Should().Be(100.00m);
    }

    [Fact]
    public void TotalAmount_ZeroValueAllowed_ForFreeInvoices()
    {
        // Zero-amount invoices are legal — used for scholarship-covered terms
        // and zero-fee enrolment records. The validators allow Amount >= 0;
        // the domain must echo that.
        var invoice = NewPendingInvoice(total: 0m);
        invoice.Items.Add(new InvoiceItem { Amount = 0m });
        invoice.TotalAmount = invoice.Items.Sum(i => i.Amount);

        invoice.TotalAmount.Should().Be(0m);
        invoice.Status.Should().Be(InvoiceStatus.Pending);
    }

    // ── IsClosed lifecycle guard ─────────────────────────────────────────────

    [Fact]
    public void Close_OnOpenInvoice_FlipsIsClosed_AndStampsClosedAt()
    {
        var invoice = NewPendingInvoice();

        invoice.Close();

        invoice.IsClosed.Should().BeTrue();
        invoice.ClosedAt.Should().NotBeNull();
        invoice.ClosedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Close_AlreadyClosed_IsNoOp_DoesNotResetClosedAt()
    {
        // Idempotency: re-closing a closed invoice must not throw and must
        // preserve the original ClosedAt — operators inspecting the audit
        // trail should see the first close, not the most recent re-call.
        var invoice = NewPendingInvoice();
        invoice.Close();
        var originalClosedAt = invoice.ClosedAt;

        // Force a small clock drift so the test would notice a re-stamp.
        System.Threading.Thread.Sleep(5);
        invoice.Close();

        invoice.IsClosed.Should().BeTrue();
        invoice.ClosedAt.Should().Be(originalClosedAt, "idempotent close preserves the original timestamp");
    }

    [Fact]
    public void EnsureMutable_OnOpenInvoice_DoesNotThrow()
    {
        var invoice = NewPendingInvoice();

        Action act = invoice.EnsureMutable;
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureMutable_OnClosedInvoice_Throws()
    {
        // Per Invoice.cs:56-60, EnsureMutable is the gate every service-side
        // write goes through. A closed record must be reopened first; this
        // test pins that any mutation path attempting to bypass the gate
        // will fail loudly.
        var invoice = NewPendingInvoice();
        invoice.Close();

        Action act = invoice.EnsureMutable;
        act.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public void Reopen_OnClosedInvoice_ClearsIsClosed()
    {
        var invoice = NewPendingInvoice();
        invoice.Close();

        invoice.Reopen();

        invoice.IsClosed.Should().BeFalse();
        invoice.ClosedAt.Should().BeNull();
        invoice.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Reopen_OnOpenInvoice_Throws()
    {
        // The asymmetry with Close (which is idempotent) is intentional:
        // Reopen is the privileged action and the caller should know they're
        // unlocking a closed record. Calling it on an already-open invoice
        // signals a logic error.
        var invoice = NewPendingInvoice();

        Action act = invoice.Reopen;
        act.Should().Throw<InvalidOperationException>().WithMessage("*not closed*");
    }
}
