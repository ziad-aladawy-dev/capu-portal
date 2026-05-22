using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.Schedule.Abstractions.Manifest;

/// <summary>
/// Declares the Schedule module's permission surface. One resource — the slot
/// itself. Action verbs mirror the CourseOffering manifest so an operator
/// granted slot-editing on a (term, node) target can attach a timetable to
/// the offerings they already manage. No <c>Open</c> verb: schedule slots
/// have no draft/published lifecycle in this iteration.
/// </summary>
public sealed class SchedulePermissionManifest : IPermissionManifest
{
    public string Module => "schedule";
    public string DisplayName => "Schedule";
    public string? Icon => "Clock";
    public int? OrderNumber => 11;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActionsNoOpen("schedule-slots", "Schedule Slots", 0),
    };
}
