namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public class ServiceScope
{
    public bool IsGlobalStructural { get; set; } = true;
    public Guid? StructureNodeId { get; set; }
    public bool IncludeDescendants { get; set; } = true;
    public string? StructureNodePath { get; set; }

    public bool IsGlobalTemporal { get; set; } = true;
    public string? Year { get; set; }
    public string? Semester { get; set; }
}