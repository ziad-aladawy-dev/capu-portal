namespace CapitalUniversity.Core.Domain.Common;

public enum LogLevelType
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
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

public enum NotificationType
{
    [Localized("معلومات", "Info")]
    Info = 1,
    [Localized("تحذير", "Warning")]
    Warning = 2,
    [Localized("خطأ", "Error")]
    Error = 3
}
public enum ActionLevel
{
    None = 0,
    View = 1,
    Insert = 2,
    EditClose = 3,
    Open = 4,
    Delete = 5
}
public enum OverrideType
{
    Allow = 1,
    Deny = 2
}