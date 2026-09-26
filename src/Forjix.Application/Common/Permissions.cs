namespace Forjix.Application.Common;

public static class Permissions
{
    public sealed record Definition(string Code, string Name, string Module);

    public const string AdministrationUsersView = "administration.users.view";
    public const string DashboardView = "dashboard.view";
    public const string AdministrationUsersManage = "administration.users.manage";
    public const string AdministrationRolesView = "administration.roles.view";
    public const string AdministrationRolesManage = "administration.roles.manage";
    public const string AuditView = "audit.view";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";
    public const string StockView = "stock.view";
    public const string StockAdjust = "stock.adjust";
    public const string StockInventoryView = "stock.inventory.view";
    public const string StockInventoryCreate = "stock.inventory.create";
    public const string StockInventoryCount = "stock.inventory.count";
    public const string StockInventoryComplete = "stock.inventory.complete";
    public const string StockInventoryCancel = "stock.inventory.cancel";
    public const string StockReports = "stock.reports";
    public const string StockManage = "stock.manage";
    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";
    public const string SalesCancel = "sales.cancel";
    public const string SalesDiscount = "sales.discount";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";
    public const string PurchasesView = "purchases.view";
    public const string PurchasesManage = "purchases.manage";
    public const string PurchasesReceive = "purchases.receive";
    public const string CashView = "cash.view";
    public const string CashManage = "cash.manage";
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string FinancialCategoriesView = "financial.categories.view";
    public const string FinancialCategoriesManage = "financial.categories.manage";
    public const string FinancialReceivableView = "financial.receivable.view";
    public const string FinancialReceivableManage = "financial.receivable.manage";
    public const string FinancialReceivablePay = "financial.receivable.pay";
    public const string FinancialPayableView = "financial.payable.view";
    public const string FinancialPayableManage = "financial.payable.manage";
    public const string FinancialPayablePay = "financial.payable.pay";
    public const string FinancialDashboardView = "financial.dashboard.view";

    public const string QuotesView = "quotes.view";
    public const string QuotesCreate = "quotes.create";
    public const string QuotesEdit = "quotes.edit";
    public const string QuotesStatus = "quotes.status";
    public const string QuotesCancel = "quotes.cancel";
    public const string QuotesConvert = "quotes.convert";
    public const string QuotesPrint = "quotes.print";

    public static readonly IReadOnlyList<string> All =
    [
        StockAdjust,
        StockInventoryView,
        StockInventoryCreate,
        StockInventoryCount,
        StockInventoryComplete,
        StockInventoryCancel,
        StockReports,
        QuotesView,
        QuotesCreate,
        QuotesEdit,
        QuotesStatus,
        QuotesCancel,
        QuotesConvert,
        QuotesPrint,
        DashboardView,
        FinancialCategoriesView,
        FinancialCategoriesManage,
        FinancialReceivableView,
        FinancialReceivableManage,
        FinancialReceivablePay,
        FinancialPayableView,
        FinancialPayableManage,
        FinancialPayablePay,
        FinancialDashboardView,
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
        ReportsView,
        ReportsExport
        ,CustomersView,
        CustomersManage
        ,SuppliersView, SuppliersManage, PurchasesView, PurchasesManage, PurchasesReceive, CashView, CashManage, SettingsView, SettingsManage
    ];

    public static readonly IReadOnlyList<Definition> Catalog =
    [
        new(StockAdjust, "Ajustar estoque", "Estoque"),
        new(StockInventoryView, "Visualizar inventários", "Estoque"),
        new(StockInventoryCreate, "Criar e editar inventários", "Estoque"),
        new(StockInventoryCount, "Iniciar e contar inventários", "Estoque"),
        new(StockInventoryComplete, "Finalizar inventários", "Estoque"),
        new(StockInventoryCancel, "Cancelar inventários", "Estoque"),
        new(StockReports, "Consultar relatórios de estoque", "Estoque"),
        new(QuotesView, "Visualizar orçamentos", "Comercial"),
        new(QuotesCreate, "Criar orçamentos", "Comercial"),
        new(QuotesEdit, "Editar orçamentos", "Comercial"),
        new(QuotesStatus, "Enviar, aprovar e rejeitar orçamentos", "Comercial"),
        new(QuotesCancel, "Cancelar orçamentos", "Comercial"),
        new(QuotesConvert, "Converter orçamentos em vendas", "Comercial"),
        new(QuotesPrint, "Imprimir orçamentos", "Comercial"),
        new(DashboardView, "Visualizar painel", "Painel"),
        new(FinancialCategoriesView, "Visualizar categorias financeiras", "Financeiro"),
        new(FinancialCategoriesManage, "Gerenciar categorias financeiras", "Financeiro"),
        new(FinancialReceivableView, "Visualizar contas a receber", "Financeiro"),
        new(FinancialReceivableManage, "Gerenciar contas a receber", "Financeiro"),
        new(FinancialReceivablePay, "Baixar e estornar contas a receber", "Financeiro"),
        new(FinancialPayableView, "Visualizar contas a pagar", "Financeiro"),
        new(FinancialPayableManage, "Gerenciar contas a pagar", "Financeiro"),
        new(FinancialPayablePay, "Baixar e estornar contas a pagar", "Financeiro"),
        new(FinancialDashboardView, "Visualizar dashboard financeiro", "Financeiro"),
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
        new(ReportsView, "Visualizar relatórios", "Relatórios"),
        new(ReportsExport, "Exportar relatórios", "Relatórios"),
        new(CustomersView, "Visualizar clientes", "Clientes"),
        new(CustomersManage, "Gerenciar clientes", "Clientes")
        ,new(SuppliersView, "Visualizar fornecedores", "Fornecedores"), new(SuppliersManage, "Gerenciar fornecedores", "Fornecedores"),
        new(PurchasesView, "Visualizar compras", "Compras"), new(PurchasesManage, "Gerenciar compras", "Compras"), new(PurchasesReceive, "Receber compras", "Compras")
        ,new(CashView,"Visualizar caixa","Caixa"),new(CashManage,"Gerenciar caixa","Caixa")
        ,new(SettingsView,"Visualizar configurações","Configurações"),new(SettingsManage,"Gerenciar configurações","Configurações")
    ];
}
