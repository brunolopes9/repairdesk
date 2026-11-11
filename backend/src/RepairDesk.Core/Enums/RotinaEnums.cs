namespace RepairDesk.Core.Enums;

/// <summary>Sprint 615 (Doc 98): o que é cada item da rotina — define o ícone e o tom dos avisos.</summary>
public enum RotinaTipo
{
    Refeicao = 1,
    Suplemento = 2,
    Meditacao = 3,
    Exercicio = 4,
    Trading = 5,
    Trabalho = 6,
    /// <summary>Limite duro (ex.: 18:00 fim do PC). Adiar obriga a dizer porquê.</summary>
    Limite = 7,
    Pessoal = 8,
    /// <summary>Registo de peso (pede o valor ao marcar como feito).</summary>
    Peso = 9,
}

public enum RotinaRegistoEstado
{
    Feito = 1,
    Saltado = 2,
    Adiado = 3,
}
