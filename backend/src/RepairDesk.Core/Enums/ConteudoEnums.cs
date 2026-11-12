namespace RepairDesk.Core.Enums;

// Sprint 598 (Doc 95): módulo Conteúdo — calendário editorial das redes sociais.

/// <summary>Ciclo de vida de uma publicação. Ver Doc 95 §4.</summary>
public enum ConteudoEstado
{
    Rascunho = 0,
    PorAprovar = 1,
    Aprovada = 2,
    /// <summary>Reservada pelo publicador automático (Fase B) — impede publicação dupla.</summary>
    APublicar = 3,
    /// <summary>Todos os destinos publicados (ou agendados na própria rede).</summary>
    Publicada = 4,
    /// <summary>Algum destino falhou depois de esgotar as tentativas.</summary>
    ComFalhas = 5,
    Rejeitada = 6,
    Cancelada = 7,
    /// <summary>Sprint 629: saiu mas foi apagada nas redes (ex.: substituída por uma versão corrigida). Não conta.</summary>
    Removida = 8,
}

/// <summary>As duas linhas de conteúdo diárias da LopesTech.</summary>
public enum ConteudoLinha
{
    Reparacoes = 0,
    Web = 1,
}

public enum ConteudoFormato
{
    Post = 0,
    Carrossel = 1,
    Reel = 2,
    Story = 3,
    /// <summary>Só texto (Threads).</summary>
    Texto = 4,
}

public enum ConteudoOrigem
{
    Manual = 0,
    /// <summary>Criada pelo Claude através da API key com scope «conteudo».</summary>
    Claude = 1,
    /// <summary>Gerada por um job do servidor (Fase C).</summary>
    Automatico = 2,
}

public enum RedeSocial
{
    Facebook = 0,
    Instagram = 1,
    Threads = 2,
    TikTok = 3,
    /// <summary>Sprint 608: YouTube Shorts (vídeos verticais até 3 min).</summary>
    YouTube = 4,
}

/// <summary>Estado de uma publicação numa rede concreta — cada rede pode falhar ou sair sozinha.</summary>
public enum ConteudoDestinoEstado
{
    Pendente = 0,
    /// <summary>Agendada na própria rede (Business Suite, TikTok Studio, Threads) — publicação manual.</summary>
    AgendadoNaRede = 1,
    Publicado = 2,
    Falhou = 3,
}

public enum ConteudoFicheiroTipo
{
    Imagem = 0,
    Video = 1,
}

public enum ConteudoFicheiroEstado
{
    /// <summary>Registo criado, binário ainda a subir para o storage.</summary>
    AEnviar = 0,
    Pronto = 1,
}
