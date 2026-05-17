using FluentValidation;

namespace RepairDesk.Services.Trabalhos;

public sealed class CreateTrabalhoValidator : AbstractValidator<CreateTrabalhoRequest>
{
    public CreateTrabalhoValidator()
    {
        RuleFor(x => x.ClienteId)
            .NotNull().WithMessage("Selecciona um cliente para o trabalho.")
            .NotEqual(Guid.Empty).WithMessage("Selecciona um cliente para o trabalho.");
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(4000);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.OrcamentoCents).GreaterThanOrEqualTo(0).When(x => x.OrcamentoCents.HasValue);
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}

public sealed class UpdateTrabalhoValidator : AbstractValidator<UpdateTrabalhoRequest>
{
    public UpdateTrabalhoValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(4000);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.OrcamentoCents).GreaterThanOrEqualTo(0).When(x => x.OrcamentoCents.HasValue);
        RuleFor(x => x.PrecoFinalCents).GreaterThanOrEqualTo(0).When(x => x.PrecoFinalCents.HasValue);
        RuleFor(x => x.HorasGastas).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}
