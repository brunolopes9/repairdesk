namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 344 (Doc 83 Pillar 3): contexto da assinatura digital recolhida do cliente.
/// </summary>
public enum SignatureType
{
    /// <summary>
    /// Cliente assina ao entregar o equipamento — autoriza diagnóstico/reparação e
    /// confirma aceitação das condições gerais. Capturada no estado Recebido.
    /// </summary>
    EntradaAutorizacao = 0,

    /// <summary>
    /// Cliente assina ao levantar — recibo de entrega confirma que recebeu o
    /// equipamento reparado em conformidade. Capturada no estado Entregue.
    /// </summary>
    EntregaLevantamento = 1,
}
