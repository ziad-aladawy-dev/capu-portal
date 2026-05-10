using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
namespace CapitalUniversity.Core.Domain.Enums;

public enum SystemTypeEnum
{
    [Localized("نظام الفصول", "Semester System")]
    SemesterSystem = 1,
    [Localized("نظام الساعات المعتمدة", "Credit Hours System")]
    CreditHoursSystem = 2
}

public enum StudentStatusEnum
{
    [Localized("نشط", "Active")]
    Active = 1,
    [Localized("متخرج", "Graduated")]
    Graduated = 2,
    [Localized("موقوف", "Suspended")]
    Suspended = 3,
    [Localized("منقول", "Transferred")]
    Transferred = 4
}

public enum ProgramTypeEnum
{
    [Localized("مستوى", "Level Based")]
    LevelBased = 1,
    [Localized("الساعات المعتمدة", "Credit Hours")]
    CreditHours = 2
}
public enum SemesterNameEnum
{
    [Localized("خريف", "Fall")]
    Fall = 1,
    [Localized("ربيع", "Spring")]
    Spring = 2,
    [Localized("صيف", "Summer")]
    Summer = 3
}