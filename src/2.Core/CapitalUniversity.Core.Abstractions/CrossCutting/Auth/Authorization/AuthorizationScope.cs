namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public class AuthorizationScope
{
    public required string Year { get; set; }
    public required string Semester { get; set; }

    public Guid? StructureNodeId { get; set; }
    public string? StructureNodePath { get; set; }
}