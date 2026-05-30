namespace RepairDesk.Core.Enums;

/// <summary>Sprint 452: canal/tipo de comunicação com o cliente.</summary>
public enum ComunicacaoTipo
{
    Nota = 0,
    Telefone = 1,
    WhatsApp = 2,
    Email = 3,
    Sms = 4,
    /// <summary>Cliente passou no balcão presencialmente.</summary>
    Visita = 5,
    /// <summary>Sprint 480: cliente escreveu pela página pública /r/{slug}. Sempre Inbound.</summary>
    PortalCliente = 6,
}

/// <summary>Sprint 452: direção da comunicação.</summary>
public enum ComunicacaoDirecao
{
    /// <summary>Cliente para nós (recebemos chamada/mensagem/email).</summary>
    Inbound = 0,
    /// <summary>Nós para cliente (enviámos mensagem, ligámos).</summary>
    Outbound = 1,
    /// <summary>Nota interna do staff (não envolveu contacto com cliente).</summary>
    Interna = 2,
}
