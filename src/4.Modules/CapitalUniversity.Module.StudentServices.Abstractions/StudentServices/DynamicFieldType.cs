namespace CapitalUniversity.Modules.StudentServices.Abstractions;

/// <summary>
/// Supported field types for a <c>ServiceFieldDefinition</c>. Stored as int —
/// values are stable and never reordered. The type drives both client-side
/// rendering and server-side <c>DynamicFieldValueValidator</c> coercion.
/// </summary>
public enum DynamicFieldType
{
    Text          = 0,
    MultilineText = 1,
    Number        = 2,
    Date          = 3,
    Boolean       = 4,
    Dropdown      = 5,
    File          = 6,
}
