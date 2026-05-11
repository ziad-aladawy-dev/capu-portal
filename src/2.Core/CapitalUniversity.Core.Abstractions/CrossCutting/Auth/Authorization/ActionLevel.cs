namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public enum ActionLevel
{
    None = 0,
    View = 1,
    Insert = 2,
    EditClose = 3,
    Open = 4,
    Delete = 5
}
