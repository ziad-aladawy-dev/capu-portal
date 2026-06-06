namespace CapitalUniversity.Sync.Student.Domain;

/// <summary>
/// External shape received from the upstream University Student system.
/// Carries the fields Core's <c>Identity.Student</c> needs for the sync
/// UPDATE path: the upstream merge key, school code, the bilingual name (the
/// mapper will normalize plain upstream text into the canonical
/// <c>{"ar":"…","en":"…"}</c> shape Core stores), and the contact / activity
/// fields.
/// <para>
/// <b>What's NOT here:</b> <c>StructureNodeKey</c>. <c>StructureNode</c> is not
/// part of the sync layer — students that don't yet exist in Core must be
/// provisioned through Core's normal admin flow (which knows the
/// <c>StructureNodeId</c>); sync is update-only for Student.
/// </para>
/// </summary>
public sealed class ExternalStudent
{
    public required string ExternalStudentId { get; init; }
    public required string StudentCode { get; init; }
    public required string Name { get; init; }
    public required string NationalId { get; init; }
    public required DateTime BirthDate { get; init; }
    public required string PhoneNumber { get; init; }
    public required string Email { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset ExternalUpdatedAt { get; init; }
    public required int ExternalVersion { get; init; }
}
