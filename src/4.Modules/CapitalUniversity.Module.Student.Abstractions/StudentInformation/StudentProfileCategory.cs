namespace CapitalUniversity.Modules.Student.Abstractions.StudentInformation;

/// <summary>
/// Coarse classification of a student profile record's contents. The schema
/// inside the JSON payload is category-specific — interpreted by the
/// consuming module, not by Student Information itself.
///
/// <para>
/// Stored as int — values are stable and never reordered.
/// </para>
/// </summary>
public enum StudentProfileCategory
{
    Custom = 0,
    MilitaryInformation = 1,
    VaccinationInformation = 2,
    EmergencyContact = 3,
    DisabilityInformation = 4,
    HousingInformation = 5,
}
