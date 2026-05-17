using FluentValidation;

namespace RepairDesk.Services.Clientes;

public sealed class CreateClienteValidator : AbstractValidator<CreateClienteRequest>
{
    public CreateClienteValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Telefone).MaximumLength(40)
            .Matches(@"^[\d\s+\-()]+$").WithMessage("Telefone inválido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Telefone));
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Nif).Matches(@"^\d{9}$").WithMessage("NIF deve ter 9 dígitos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nif));
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}

public sealed class UpdateClienteValidator : AbstractValidator<UpdateClienteRequest>
{
    public UpdateClienteValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Telefone).MaximumLength(40)
            .Matches(@"^[\d\s+\-()]+$").WithMessage("Telefone inválido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Telefone));
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Nif).Matches(@"^\d{9}$").WithMessage("NIF deve ter 9 dígitos.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nif));
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}
