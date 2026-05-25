using FluentValidation;

namespace RepairDesk.Services.Despesas;

public sealed class CreateDespesaValidator : AbstractValidator<CreateDespesaRequest>
{
    public CreateDespesaValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.ValorCents).GreaterThan(0);
        RuleFor(x => x.Fornecedor).MaximumLength(200);
        RuleFor(x => x.NumeroEncomenda).MaximumLength(100);
        RuleFor(x => x.Notas).MaximumLength(1000);
        RuleFor(x => x.PeriodicidadeMeses)
            .Must(x => x is null or 1 or 3 or 12)
            .WithMessage("PeriodicidadeMeses deve ser 1, 3 ou 12.");
        RuleFor(x => x.PeriodicidadeMeses)
            .NotNull()
            .When(x => x.IsRecorrente)
            .WithMessage("PeriodicidadeMeses e obrigatoria em despesas recorrentes.");
    }
}

public sealed class UpdateDespesaValidator : AbstractValidator<UpdateDespesaRequest>
{
    public UpdateDespesaValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.ValorCents).GreaterThan(0);
        RuleFor(x => x.Fornecedor).MaximumLength(200);
        RuleFor(x => x.NumeroEncomenda).MaximumLength(100);
        RuleFor(x => x.Notas).MaximumLength(1000);
        RuleFor(x => x.PeriodicidadeMeses)
            .Must(x => x is null or 1 or 3 or 12)
            .WithMessage("PeriodicidadeMeses deve ser 1, 3 ou 12.");
        RuleFor(x => x.PeriodicidadeMeses)
            .NotNull()
            .When(x => x.IsRecorrente)
            .WithMessage("PeriodicidadeMeses e obrigatoria em despesas recorrentes.");
    }
}
