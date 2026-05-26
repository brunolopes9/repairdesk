using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 344: persistência de assinaturas digitais.</summary>
public interface ISignatureRepository
{
    Task<SignatureCapture?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SignatureCapture>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(SignatureCapture signature, CancellationToken ct = default);
    Task DeleteAsync(SignatureCapture signature, CancellationToken ct = default);
}
