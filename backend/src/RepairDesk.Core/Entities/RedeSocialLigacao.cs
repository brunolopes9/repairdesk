using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 599 (Doc 95 Fase B): conta de uma rede social ligada por OAuth. Uma linha por rede:
/// Facebook (Página + token da Página, não expira), Instagram (conta profissional ligada à mesma
/// Página, usa o token da Página) e Threads (token de 60 dias, renovado automaticamente).
/// O token nunca sai do servidor.
/// </summary>
public class RedeSocialLigacao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public RedeSocial Rede { get; set; }

    /// <summary>Id da Página (FB), da conta IG profissional ou do utilizador Threads.</summary>
    public required string ContaId { get; set; }
    /// <summary>Nome visível (nome da Página, @utilizador).</summary>
    public required string ContaNome { get; set; }

    public required string TokenCipherText { get; set; }
    /// <summary>Null = não expira (token de Página).</summary>
    public DateTime? TokenExpiraEm { get; set; }

    public DateTime LigadaEm { get; set; } = DateTime.UtcNow;
    public Guid? LigadaPorUserId { get; set; }

    /// <summary>False quando a rede recusou o token (erro 190) — é preciso ligar outra vez.</summary>
    public bool Valida { get; set; } = true;
    public string? UltimoErro { get; set; }
}
