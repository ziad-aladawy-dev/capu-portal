using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.CourseOffering.Abstractions;

namespace CapitalUniversity.Modules.CourseOffering.Domain;

/// <summary>
/// Runtime availability of a catalog <c>Course</c> for one academic term
/// (<c>Semester</c>) targeting one organizational node (<c>StructureNode</c>),
/// optionally split across sections. The offering is the bridge between the
/// curriculum (Course / AcademicPlan) and the things that will later attach
/// to it: schedules, registrations, instructors, rooms.
///
/// <para>
/// Per <c>docs/CourseOffering_Model.md</c>, this entity owns runtime
/// availability + registration state + offering lifecycle only. Schedules
/// belong to <c>ScheduleSlot</c> children (not yet implemented). It must NOT
/// grow registration engines, fee logic, transcript logic, or workflow
/// orchestration.
/// </para>
///
/// <para>
/// All cross-module references are by id with no EF navigation, matching the
/// modularity rule used by <c>AcademicPlan</c> / <c>Invoice</c> /
/// <c>StudentProfileRecord</c>.
/// </para>
/// </summary>
public class CourseOffering : BaseEntity, ISoftDeletable
{
    /// <summary>Catalog course this offering opens. FK-by-id; no nav.</summary>
    public Guid CourseId { get; set; }

    /// <summary>Academic term the offering runs in. FK-by-id; no nav.</summary>
    public Guid SemesterId { get; set; }

    /// <summary>Organizational target (typically the program/department the offering serves). FK-by-id; no nav.</summary>
    public Guid StructureNodeId { get; set; }

    /// <summary>Section identifier — disambiguates multiple offerings of the same course in the same term + node (e.g. <c>"A"</c>, <c>"B"</c>, <c>"EVE-1"</c>).</summary>
    public string SectionCode { get; set; } = string.Empty;

    /// <summary>Maximum number of registrations allowed. Non-negative. Capacity cuts below <see cref="RegisteredCount"/> are rejected by <see cref="AdjustCapacity"/>.</summary>
    public int Capacity { get; private set; }

    /// <summary>Current count of held registrations. Maintained through <see cref="IncrementRegistration"/> / <see cref="DecrementRegistration"/> only — never assigned directly to keep the capacity invariant intact.</summary>
    public int RegisteredCount { get; private set; }

    private OfferingStatus _status = OfferingStatus.Draft;
    private RegistrationState _registrationState = RegistrationState.Closed;

    /// <summary>Lifecycle stage of the offering. The <c>init</c> setter keeps the Create-time object initializer (and EF hydration) working; later transitions go through <see cref="Activate"/> / <see cref="Close"/> / <see cref="Cancel"/> so illegal jumps (e.g. re-opening a closed offering) cannot slip through a raw assignment.</summary>
    public OfferingStatus Status
    {
        get => _status;
        init => _status = value;
    }

    /// <summary>Whether registration is currently accepting students. <c>init</c> for construction + EF; later transitions go through <see cref="OpenRegistration"/> / <see cref="CloseRegistration"/> / <see cref="SetWaitlist"/> so the offering-level rule "registration requires an active offering" cannot be bypassed.</summary>
    public RegistrationState RegistrationState
    {
        get => _registrationState;
        init => _registrationState = value;
    }

    /// <summary>Identifier in an upstream system if this offering mirrors one. Optional.</summary>
    public string? ExternalSystemId { get; set; }

    /// <summary>Timestamp of the last successful sync from the upstream system. Optional.</summary>
    public DateTime? ExternalSyncedAt { get; set; }

    /// <summary>SQL Server <c>rowversion</c>. EF maps this to optimistic concurrency — co-edits on capacity/state must not silently overwrite each other.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Construction-time initializer. Validation that requires the request DTO
    /// belongs in the validator; this method enforces only the entity-local
    /// invariant that capacity is non-negative and starting count is zero.
    /// </summary>
    public void InitializeCapacity(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative.");
        Capacity = capacity;
        RegisteredCount = 0;
    }

    /// <summary>
    /// Adjust capacity to a new value. Rejects shrinking below the current
    /// registration count — that's a Registration-module concern (de-register
    /// students first), not a silent data loss here.
    /// </summary>
    public void AdjustCapacity(int newCapacity)
    {
        if (newCapacity < 0) throw new ArgumentOutOfRangeException(nameof(newCapacity), "Capacity must be non-negative.");
        if (newCapacity < RegisteredCount)
        {
            throw new InvalidOperationException("Capacity cannot be reduced below the current registration count.");
        }
        Capacity = newCapacity;
    }

    /// <summary>
    /// In-memory invariant enforcement for the registration count. Rejects if
    /// the offering is cancelled (offering-level concern, not registration
    /// policy) or if it would exceed capacity.
    ///
    /// <para>
    /// <b>Concurrency note:</b> this method is for single-request paths and
    /// invariant tests. Call sites that may contend (the future Registration
    /// module during a registration window) must use
    /// <c>ICourseOfferingRepository.TryIncrementRegistrationAsync</c> instead —
    /// it wraps the same guard in optimistic-concurrency + bounded retry so
    /// two simultaneous claims on the last seat can never both succeed.
    /// </para>
    /// </summary>
    public void IncrementRegistration()
    {
        if (_status == OfferingStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled offerings cannot accept registrations.");
        }
        if (RegisteredCount >= Capacity)
        {
            throw new InvalidOperationException("Offering is full.");
        }
        RegisteredCount += 1;
    }

    /// <summary>Drop the registration count by one. Floored at zero so a double-drop cannot make the count negative.</summary>
    public void DecrementRegistration()
    {
        if (RegisteredCount == 0) return;
        RegisteredCount -= 1;
    }

    // -----------------------------------------------------------------------
    // Lifecycle transitions. State-machine rules live on the entity so the
    // service (and any future caller, including the Registration module) cannot
    // bypass them. Each method is idempotent on the target state — repeated
    // Activate() / Close() / Cancel() calls are safe.
    // -----------------------------------------------------------------------

    /// <summary>Draft → Open. Already-Open is a no-op. Reopening a Closed or Cancelled offering is rejected — that's a "create a new offering" decision, not a state flip.</summary>
    public void Activate()
    {
        if (_status == OfferingStatus.Open) return;
        if (_status != OfferingStatus.Draft)
        {
            throw new InvalidOperationException("Only draft offerings can be activated.");
        }
        _status = OfferingStatus.Open;
    }

    /// <summary>Open → Closed (registration auto-shuts to prevent stale "Open registration" on a closed offering). Already-Closed is a no-op. Closing a Draft (never opened) or Cancelled offering is rejected.</summary>
    public void Close()
    {
        if (_status == OfferingStatus.Closed) return;
        if (_status == OfferingStatus.Draft)
        {
            throw new InvalidOperationException("Draft offerings cannot be closed without first being activated.");
        }
        if (_status == OfferingStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled offerings cannot be closed.");
        }
        _status = OfferingStatus.Closed;
        _registrationState = RegistrationState.Closed;
    }

    /// <summary>Any non-cancelled state → Cancelled. Registration is forced to Closed so cancellation cannot leave a "still accepting students" flag dangling. Already-Cancelled is a no-op.</summary>
    public void Cancel()
    {
        if (_status == OfferingStatus.Cancelled) return;
        _status = OfferingStatus.Cancelled;
        _registrationState = RegistrationState.Closed;
    }

    /// <summary>Flip registration to Open. Requires an active (<see cref="OfferingStatus.Open"/>) offering — registration on Draft / Closed / Cancelled would mean either "accepting before launch" or "accepting after shutdown", both wrong.</summary>
    public void OpenRegistration()
    {
        if (_status != OfferingStatus.Open)
        {
            throw new InvalidOperationException("Registration can only be opened on an active offering.");
        }
        _registrationState = RegistrationState.Open;
    }

    /// <summary>Flip registration to Closed. Always allowed — safe to call defensively from any state, including Cancelled.</summary>
    public void CloseRegistration()
    {
        _registrationState = RegistrationState.Closed;
    }

    /// <summary>Flip registration to Waitlist. Requires an active offering — same rationale as <see cref="OpenRegistration"/>.</summary>
    public void SetWaitlist()
    {
        if (_status != OfferingStatus.Open)
        {
            throw new InvalidOperationException("Waitlist requires an active offering.");
        }
        _registrationState = RegistrationState.Waitlist;
    }

    // Future-readiness note (no code yet): when ScheduleSlot is added, it will
    // attach as a child collection here (`ICollection<ScheduleSlot> Slots`) with
    // cascade delete on the offering, mirroring AcademicPlan → AcademicPlanCourse.
    // No structural change is needed today — this entity already lives in a
    // module that owns its own EF configuration and can introduce the child
    // table independently.
}
