namespace RepairDesk.Core.Enums;

/// <summary>Sprint 613 (Doc 97): categoria da loja no lopestech.pt (também é o segmento do URL).</summary>
public enum LojaCategoria
{
    Smartphones = 0,
    Computadores = 1,
    Tablets = 2,
    Consolas = 3,
    Acessorios = 4,
    Outros = 5,
}

/// <summary>Estado do equipamento — define a condição no Google (New/Refurbished/Used) e a garantia mínima.</summary>
public enum LojaEstado
{
    Novo = 0,
    NovoCaixaAberta = 1,
    Recondicionado = 2,
    Usado = 3,
}

/// <summary>Grau estético (só recondicionados/usados).</summary>
public enum LojaGrau
{
    Premium = 0,
    Excelente = 1,
    MuitoBom = 2,
    Bom = 3,
}

/// <summary>De onde vem: stock próprio (pronto a enviar) ou oferta do fornecedor (por encomenda).</summary>
public enum LojaOrigem
{
    Proprio = 0,
    Fornecedor = 1,
}

public enum LojaDisponibilidade
{
    EmStock = 0,
    PorEncomenda = 1,
    Esgotado = 2,
}

/// <summary>Regime de IVA da venda — Margem = bens em segunda mão (IVA só sobre a margem, DL 199/96).</summary>
public enum LojaRegimeIva
{
    Normal = 0,
    Margem = 1,
}
