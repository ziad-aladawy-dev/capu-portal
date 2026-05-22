using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.UniversityStructure.Enums;

public enum StructureNodeType
{
    [Localized("جامعة", "University")]
    University = 1,

    [Localized("كلية", "Faculty")]
    Faculty = 2,

    [Localized("نظام", "System")]
    System = 3,

    [Localized("برنامج", "Program")]
    Program = 4,

    [Localized("مستوى", "Level")]
    Level = 5,

    [Localized("قسم", "Department")]
    Department = 6,

    [Localized("تخصص", "Specialization")]
    Specialization = 7,
}