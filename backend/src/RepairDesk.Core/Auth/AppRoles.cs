namespace RepairDesk.Core.Auth;

/// <summary>
/// Sprint 311 (Doc 72 Fase D): constants para roles. Centralizar evita strings espalhadas
/// pelos controllers e dá compile-time safety para o policy builder.
///
/// Hierarquia conceptual:
/// <list type="bullet">
///   <item><see cref="Admin"/> — acesso total (fiscal, fornecedores, peças, fatura, RGPD).</item>
///   <item><see cref="Tech"/> — reparações, diagnóstico, peças (sem fatura/fiscal).</item>
///   <item><see cref="Cashier"/> — vendas POS, caixa, fatura, clientes (sem reparações).</item>
///   <item><see cref="ReadOnly"/> — leitura apenas (dashboard, histórico).</item>
/// </list>
///
/// Roles NÃO são mutuamente exclusivas — um user pode ter Tech+Cashier. Endpoint de
/// atribuição vai aceitar lista de roles.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Tech = "Tech";
    public const string Cashier = "Cashier";
    public const string ReadOnly = "ReadOnly";

    /// <summary>Todas as roles atribuíveis (seed + atribuição).</summary>
    public static readonly IReadOnlyList<string> All = new[] { Admin, Tech, Cashier, ReadOnly };
}

/// <summary>
/// Nomes das policies para <c>[Authorize(Policy = ...)]</c>. Policies combinam múltiplas
/// roles num único nome, ex: <c>RequireTechOrAdmin</c> permite Tech OU Admin.
/// </summary>
public static class AppPolicies
{
    /// <summary>Apenas Admin. Equivalente ao <c>[Authorize(Roles = "Admin")]</c> existente.</summary>
    public const string RequireAdmin = "RequireAdmin";

    /// <summary>Admin OU Tech. Para operações de reparação / peças / diagnóstico.</summary>
    public const string RequireTechOrAdmin = "RequireTechOrAdmin";

    /// <summary>Admin OU Cashier. Para POS / vendas / caixa / faturação.</summary>
    public const string RequireCashierOrAdmin = "RequireCashierOrAdmin";

    /// <summary>Admin OU Tech OU Cashier. Quem mexe em algo (não-readonly).</summary>
    public const string RequireWrite = "RequireWrite";
}
