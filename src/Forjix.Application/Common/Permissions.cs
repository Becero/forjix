namespace Forjix.Application.Common;

public static class Permissions
{
    public const string AdministrationUsersView = "administration.users.view";
    public const string AdministrationUsersManage = "administration.users.manage";
    public const string AdministrationRolesView = "administration.roles.view";
    public const string AdministrationRolesManage = "administration.roles.manage";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<string> All =
    [
        AdministrationUsersView,
        AdministrationUsersManage,
        AdministrationRolesView,
        AdministrationRolesManage,
        AuditView
    ];
}
