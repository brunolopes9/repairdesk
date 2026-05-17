using FluentValidation;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Parts;

public sealed class CreatePartValidator : AbstractValidator<CreatePartRequest>
{
    public CreatePartValidator()
    {
        RuleFor(x => x.Sku).MaximumLength(80);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.Marca).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(140);
        RuleFor(x => x.QtdStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QtdMinima).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CustoUnitarioCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fornecedor).MaximumLength(200);
        RuleFor(x => x.LocalArmazenamento).MaximumLength(120);
        RuleFor(x => x.Notas).MaximumLength(1000);
    }
}

public sealed class UpdatePartValidator : AbstractValidator<UpdatePartRequest>
{
    public UpdatePartValidator()
    {
        RuleFor(x => x.Sku).MaximumLength(80);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Categoria).IsInEnum();
        RuleFor(x => x.Marca).MaximumLength(100);
        RuleFor(x => x.Modelo).MaximumLength(140);
        RuleFor(x => x.QtdStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QtdMinima).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CustoUnitarioCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fornecedor).MaximumLength(200);
        RuleFor(x => x.LocalArmazenamento).MaximumLength(120);
        RuleFor(x => x.Notas).MaximumLength(1000);
    }
}

public sealed class CreatePartMovimentoValidator : AbstractValidator<CreatePartMovimentoRequest>
{
    public CreatePartMovimentoValidator()
    {
        RuleFor(x => x.Quantidade).NotEqual(0);
        RuleFor(x => x.Motivo).IsInEnum();
        RuleFor(x => x.ReparacaoId)
            .NotNull()
            .When(x => x.Motivo == PartMovimentoMotivo.UsoEmReparacao)
            .WithMessage("ReparacaoId e obrigatorio quando a peça e usada numa reparacao.");
        RuleFor(x => x.Notas).MaximumLength(1000);
    }
}
