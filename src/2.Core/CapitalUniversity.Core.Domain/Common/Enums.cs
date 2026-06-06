namespace CapitalUniversity.Core.Domain.Common;

public enum LogLevelType
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
/// Origin classification for an audit/log entry, used as a top-level filter
/// dimension on the audit read API. Orthogonal to <see cref="LogLevelType"/>
/// (severity): a Sync entry can be an Error, an entry with no explicit category
/// defaults to <see cref="Data"/> for Info and <see cref="Error"/> for
/// Warning/Error/Critical.
/// </summary>
public enum LogCategory
{
    /// <summary>Entity state change captured by the EF audit trail (Created/Updated/Deleted).</summary>
    Data = 1,
    /// <summary>Security/authentication event (login, logout, permission denied, role change…).</summary>
    Auth = 2,
    /// <summary>Emitted by the Sync platform (background jobs writing into Core).</summary>
    Sync = 3,
    /// <summary>Application errors and warnings not otherwise categorised.</summary>
    Error = 4
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