namespace CapitalUniversity.Core.Domain.Common;

public enum LogLevelType
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
public enum SystemType
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

public enum ProgramType
{
    [Localized("مستوى", "Level Based")]
    LevelBased = 1,
    [Localized("الساعات المعتمدة", "Credit Hours")]
    CreditHours = 2
}
public enum SemesterName
{
    [Localized("خريف", "Fall")]
    Fall = 1,
    [Localized("ربيع", "Spring")]
    Spring = 2,
    [Localized("صيف", "Summer")]
    Summer = 3
}

public enum NotificationType
{
    [Localized("معلومات", "Info")]
    Info = 1,
    [Localized("تحذير", "Warning")]
    Warning = 2
}
public enum OverrideType
{
    [Localized("سماح", "Allow")]
    Allow = 1,
    [Localized("منع", "Deny")]
    Deny = 2
}