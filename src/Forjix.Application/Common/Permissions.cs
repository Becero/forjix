namespace Forjix.Application.Common;

public static class Permissions
{
    public sealed record Definition(string Code, string Name, string Module);

    public const string AdministrationUsersView = "administration.users.view";
    public const string AdministrationUsersManage = "administration.users.manage";
    public const string AdministrationRolesView = "administration.roles.view";
    public const string AdministrationRolesManage = "administration.roles.manage";
    public const string AuditView = "audit.view";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";
    public const string StockView = "stock.view";
    public const string StockManage = "stock.manage";
    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";
    public const string SalesCancel = "sales.cancel";
    public const string SalesDiscount = "sales.discount";
    public const string ReportsView = "reports.view";

    public static readonly IReadOnlyList<string> All =
    [
        AdministrationUsersView,
        AdministrationUsersManage,
        AdministrationRolesView,
        AdministrationRolesManage,
        AuditView,
        ProductsView,
        ProductsManage,
        CategoriesView,
        CategoriesManage,
        StockView,
        StockManage,
        SalesView,
        SalesCreate,
        SalesCancel,
        SalesDiscount,
        ReportsView
    ];

    public static readonly IReadOnlyList<Definition> Catalog =
    [
        new(AdministrationUsersView, "Visualizar usuários", "Administração"),
        new(AdministrationUsersManage, "Gerenciar usuários", "Administração"),
        new(AdministrationRolesView, "Visualizar grupos de acesso", "Administração"),
        new(AdministrationRolesManage, "Gerenciar grupos e permissões", "Administração"),
        new(AuditView, "Visualizar auditoria", "Segurança"),
        new(ProductsView, "Visualizar produtos", "Produtos"),
        new(ProductsManage, "Cadastrar e editar produtos", "Produtos"),
        new(CategoriesView, "Visualizar categorias", "Categorias"),
        new(CategoriesManage, "Gerenciar categorias", "Categorias"),
        new(StockView, "Visualizar movimentações", "Estoque"),
        new(StockManage, "Registrar movimentações", "Estoque"),
        new(SalesView, "Visualizar vendas", "Vendas"),
        new(SalesCreate, "Realizar vendas", "Vendas"),
        new(SalesCancel, "Cancelar vendas", "Vendas"),
        new(SalesDiscount, "Conceder desconto", "Vendas"),
        new(ReportsView, "Visualizar relatórios", "Relatórios")
    ];
}
