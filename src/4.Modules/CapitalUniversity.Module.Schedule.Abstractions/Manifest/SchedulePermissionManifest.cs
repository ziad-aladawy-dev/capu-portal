using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.Schedule.Abstractions.Manifest;

/// <summary>
/// Declares the Schedule module's permission surface. One resource — the slot
/// itself. Action verbs mirror the CourseOffering manifest so an operator
/// granted slot-editing on a (term, node) target can attach a timetable to
/// the offerings they already manage.
/// </summary>
public sealed class SchedulePermissionManifest : IPermissionManifest
{
    private const string ResourceScheduleSlots = "schedule-slots";
    private const string DisplayScheduleSlots = "Schedule Slots";

    public string Module => "schedule";
    public string DisplayName => "Schedule";
    public string? Icon => "Clock";
    public int? OrderNumber => 11;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create(ResourceScheduleSlots, "View",      DisplayScheduleSlots, 0),
        PermissionDefinition.Create(ResourceScheduleSlots, "Insert",    DisplayScheduleSlots, 0),
        PermissionDefinition.Create(ResourceScheduleSlots, "EditClose", DisplayScheduleSlots, 0),
        PermissionDefinition.Create(ResourceScheduleSlots, "Delete",    DisplayScheduleSlots, 0),
    };
}
