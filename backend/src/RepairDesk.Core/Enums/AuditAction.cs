namespace RepairDesk.Core.Enums;

public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    HardDelete = 3,
    Login = 4,
    Export = 5,
    Restore = 6,
    LoginFailed = 7,
    Logout = 8,
    UserDeactivated = 9,
}
