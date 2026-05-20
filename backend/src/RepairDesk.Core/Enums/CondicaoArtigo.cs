namespace RepairDesk.Core.Enums;

/// <summary>
/// Condição do artigo vendido. Afecta a garantia ao cliente (DL 84/2021): refurbished/usado
/// pode ser 18 meses com acordo expresso, novo é 3 anos. Snapshot na venda — independente
/// do Part actual (fornecedor/condição podem mudar; vendas antigas mantêm o histórico).
/// </summary>
public enum CondicaoArtigo
{
    NaoAplicavel = 0,
    Novo = 1,
    OpenBox = 2,
    Recondicionado = 3,
    Usado = 4,
}
