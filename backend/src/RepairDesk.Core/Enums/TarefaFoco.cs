namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 617 (Doc 98 F2): onde a tarefa está na cabeça do utilizador.
/// Hoje = prioridade do dia (máximo 3 por pessoa); Semana = esta semana; Ideia = capturada para mais tarde.
/// </summary>
public enum TarefaFoco
{
    Nenhum = 0,
    Hoje = 1,
    Semana = 2,
    Ideia = 3,
}
