using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs.Management;

/// <summary>
/// Represents a module in the permission tree.
/// </summary>
public class ModulePermissionTreeDto
{
    /// <summary>
    /// The unique identifier of the module.
    /// </summary>
    public Guid ModuleId { get; set; }

    /// <summary>
    /// The display name of the module.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// The list of resources (services) within this module.
    /// </summary>
    public List<ResourcePermissionTreeDto> Resources { get; set; } = new();
}

/// <summary>
/// Represents a resource (service) in the permission tree.
/// </summary>
public class ResourcePermissionTreeDto
{
    /// <summary>
    /// The unique identifier of the resource.
    /// </summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// The display name of the resource.
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// The list of discrete permissions (actions) available for this resource.
    /// </summary>
    public List<PermissionActionDto> Permissions { get; set; } = new();
}

/// <summary>
/// Represents a discrete permission (action) for a resource.
/// </summary>
public class PermissionActionDto
{
    /// <summary>
    /// A unique identifier for the permission (usually ServiceId_Level).
    /// </summary>
    public string PermissionId { get; set; } = string.Empty;

    /// <summary>
    /// The canonical name of the permission (e.g., "module.resource.action").
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;

    /// <summary>
    /// The action name (e.g., "Read", "Create").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable description of the permission.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the permission is assigned to the requested context (e.g., a specific role).
    /// Only populated when querying for a specific role or user.
    /// </summary>
    public bool? IsAssigned { get; set; }
}
