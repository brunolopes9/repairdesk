namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 633 (Doc 99 — Finanças do dono): a quem pertence a despesa.
/// Negocio = gasto da atividade (entra no IVA/IRS conforme a afetação); Pessoal = vida pessoal (fora de tudo o que é
/// fiscal, só para o dono saber «a quantas anda»); Obrigacao = Segurança Social e pagamentos de IVA/IRS (tesouraria).
/// </summary>
public enum DespesaAmbito
{
    Negocio = 0,
    Pessoal = 1,
    Obrigacao = 2,
}

/// <summary>Sprint 633: classificação do Excel do Bruno — conta igual no resultado, mostra-se separado.</summary>
public enum DespesaNatureza
{
    Gasto = 0,
    Investimento = 1,
}
