using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Domain;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.CourseOfferings;

/// <summary>
/// State-machine guarantees on <see cref="CourseOffering"/>: only legal
/// lifecycle transitions are accepted; idempotent on the same target state;
/// registration state cannot be opened when the offering is not active;
/// cancellation forces registration closed.
///
/// <para>
/// <b>Exception-type contract.</b> The domain throws
/// <see cref="InvalidOperationException"/> for state-machine and invariant
/// violations (e.g. "draft offering cannot be closed", "registration
/// requires an active offering", "registration on cancelled offering").
/// <see cref="ConflictException"/> is reserved for the closed-record guard
/// in <c>EnsureMutable</c> — when <c>IsClosed == true</c>, every mutating
/// method short-circuits there before reaching its own state checks. The
/// service layer (<c>CourseOfferingService.ApplyStatusChange</c> etc.)
/// catches <see cref="InvalidOperationException"/> and surfaces it to the
/// API as <see cref="ConflictException"/> with a localized message key — so
/// callers above the service still see HTTP 409 with a localized body.
/// </para>
/// </summary>
public class CourseOfferingLifecycleTests
{
    private static CourseOffering NewDraftOffering()
    {
        var offering = new CourseOffering
        {
            CourseId = Guid.NewGuid(),
            SemesterId = Guid.NewGuid(),
            StructureNodeId = Guid.NewGuid(),
            SectionCode = "A",
        };
        offering.InitializeCapacity(10);
        return offering;
    }

    private static CourseOffering NewOpenOffering()
    {
        var offering = NewDraftOffering();
        offering.Activate();
        return offering;
    }

    // ---- Status transitions ------------------------------------------------

    [Fact]
    public void Activate_FromDraft_GoesOpen()
    {
        var offering = NewDraftOffering();
        offering.Activate();
        offering.Status.Should().Be(OfferingStatus.Open);
    }

    [Fact]
    public void Activate_AlreadyOpen_IsNoOp()
    {
        var offering = NewOpenOffering();
        offering.Activate();
        offering.Status.Should().Be(OfferingStatus.Open);
    }

    [Fact]
    public void Activate_FromClosed_Throws()
    {
        var offering = NewOpenOffering();
        offering.Close();
        var act = offering.Activate;
        act.Should().Throw<ConflictException>().WithMessage("*closed*");
    }

    [Fact]
    public void Activate_FromCancelled_Throws()
    {
        // Cancel() does not set IsClosed, so EnsureMutable() passes and the
        // state-machine guard in Activate() fires. Per the exception-type
        // contract on the class, that's an InvalidOperationException which
        // the service layer wraps for the API.
        var offering = NewDraftOffering();
        offering.Cancel();
        var act = offering.Activate;
        act.Should().Throw<InvalidOperationException>().WithMessage("*draft*");
    }

    [Fact]
    public void Close_FromOpen_GoesClosedAndShutsRegistration()
    {
        var offering = NewOpenOffering();
        offering.OpenRegistration();

        offering.Close();

        offering.Status.Should().Be(OfferingStatus.Closed);
        offering.RegistrationState.Should().Be(RegistrationState.Closed, "registration must auto-shut to prevent a stale 'open' flag on a closed offering");
    }

    [Fact]
    public void Close_FromDraft_Throws()
    {
        // Draft → Closed is not a legal transition. IsClosed is false so
        // EnsureMutable() doesn't fire; the state-machine guard does. The
        // service maps this to ConflictException + IllegalStateTransition
        // for the API.
        var offering = NewDraftOffering();
        var act = offering.Close;
        act.Should().Throw<InvalidOperationException>().WithMessage("*activated*");
    }

    [Fact]
    public void Close_FromCancelled_Throws()
    {
        // Cancelled → Closed is not a legal transition. Cancel() leaves
        // IsClosed false, so EnsureMutable() passes; the state-machine guard
        // in Close() throws.
        var offering = NewDraftOffering();
        offering.Cancel();
        var act = offering.Close;
        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelled*");
    }

    [Fact]
    public void Cancel_FromAnyNonCancelled_GoesCancelledAndShutsRegistration()
    {
        var offering = NewOpenOffering();
        offering.OpenRegistration();

        offering.Cancel();

        offering.Status.Should().Be(OfferingStatus.Cancelled);
        offering.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_IsNoOp()
    {
        var offering = NewDraftOffering();
        offering.Cancel();
        offering.Cancel();
        offering.Status.Should().Be(OfferingStatus.Cancelled);
    }

    // ---- Registration-state transitions -----------------------------------

    [Fact]
    public void OpenRegistration_RequiresActiveOffering()
    {
        // Registration cannot be opened on a Draft offering. EnsureMutable()
        // passes (IsClosed=false), so the registration-state guard throws
        // InvalidOperationException — service translates to ConflictException
        // at the API edge.
        var draft = NewDraftOffering();
        var act = draft.OpenRegistration;
        act.Should().Throw<InvalidOperationException>().WithMessage("*active*");
    }

    [Fact]
    public void OpenRegistration_OnOpen_Succeeds()
    {
        var offering = NewOpenOffering();
        offering.OpenRegistration();
        offering.RegistrationState.Should().Be(RegistrationState.Open);
    }

    [Fact]
    public void SetWaitlist_RequiresActiveOffering()
    {
        // Waitlist requires an active offering. Same shape as OpenRegistration
        // above: InvalidOperationException at the domain, ConflictException at
        // the API after service translation.
        var draft = NewDraftOffering();
        var act = draft.SetWaitlist;
        act.Should().Throw<InvalidOperationException>().WithMessage("*active*");
    }

    [Fact]
    public void CloseRegistration_AlwaysAllowed()
    {
        var offering = NewDraftOffering();
        offering.CloseRegistration(); // already Closed — safe no-op
        offering.RegistrationState.Should().Be(RegistrationState.Closed);

        offering.Cancel();
        offering.CloseRegistration(); // even on Cancelled — defensive safety net
        offering.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public void CloseRegistration_FromOpen_ActuallyClosesIt()
    {
        // The existing CloseRegistration_AlwaysAllowed test only calls
        // CloseRegistration when the registration state is already Closed,
        // so a mutation that empties the method body still leaves state as
        // Closed and survives. This test opens registration first so the
        // method's body MUST execute to observe the transition.
        var offering = NewOpenOffering();
        offering.OpenRegistration();
        offering.RegistrationState.Should().Be(RegistrationState.Open);

        offering.CloseRegistration();

        offering.RegistrationState.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public void Close_AlreadyClosed_IsNoOp()
    {
        // The early-return guard in Close (`if (_status == Closed) return`)
        // is currently unreached by any test — Close_FromOpen tests the
        // happy transition; Draft/Cancelled tests hit the throw branches.
        // Re-calling Close on a closed offering must not throw and must
        // leave the state unchanged.
        var offering = NewOpenOffering();
        offering.Close();
        offering.Status.Should().Be(OfferingStatus.Closed);

        var act = offering.Close; // second call
        act.Should().NotThrow("idempotent Close on an already-closed offering must not throw");
        offering.Status.Should().Be(OfferingStatus.Closed);
    }

    // ---- Cross-invariant: registration count + cancellation ---------------

    [Fact]
    public void IncrementRegistration_OnCancelledOffering_Throws()
    {
        // IncrementRegistration is an invariant-only path (no EnsureMutable —
        // see the concurrency note on the method). Cancelled offerings reject
        // registration as InvalidOperationException; the future Registration
        // module is the caller and will surface this as a 409 itself.
        var offering = NewDraftOffering();
        offering.Cancel();
        var act = offering.IncrementRegistration;
        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelled*");
        offering.RegisteredCount.Should().Be(0);
    }
}