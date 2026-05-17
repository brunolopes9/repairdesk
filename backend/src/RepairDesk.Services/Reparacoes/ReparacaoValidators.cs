using FluentValidation;

namespace RepairDesk.Services.Reparacoes;

public sealed class CreateReparacaoValidator : AbstractValidator<CreateReparacaoRequest>
{
    public CreateReparacaoValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty();
        RuleFor(x => x.Equipamento).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Avaria).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Imei).MaximumLength(40);
        RuleFor(x => x.OrcamentoCents).GreaterThanOrEqualTo(0).When(x => x.OrcamentoCents.HasValue);
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}

public sealed class UpdateReparacaoValidator : AbstractValidator<UpdateReparacaoRequest>
{
    public UpdateReparacaoValidator()
    {
        RuleFor(x => x.Equipamento).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Avaria).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Imei).MaximumLength(40);
        RuleFor(x => x.Diagnostico).MaximumLength(2000);
        RuleFor(x => x.OrcamentoCents).GreaterThanOrEqualTo(0).When(x => x.OrcamentoCents.HasValue);
        RuleFor(x => x.PrecoFinalCents).GreaterThanOrEqualTo(0).When(x => x.PrecoFinalCents.HasValue);
        RuleFor(x => x.CustoPecasCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.HorasGastas).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}

public sealed class ChangeEstadoValidator : AbstractValidator<ChangeEstadoRequest>
{
    public ChangeEstadoValidator()
    {
        RuleFor(x => x.Estado).IsInEnum();
        RuleFor(x => x.Notas).MaximumLength(1000);
    }
}
