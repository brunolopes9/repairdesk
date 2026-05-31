using FluentValidation;
using RepairDesk.Common.Helpers;

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
        RuleFor(x => x.Nif)
            .Must(nif => NifValidator.IsValid(nif))
            .WithMessage("NIF inválido — verifica os 9 dígitos e o check-digit.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nif));
        RuleFor(x => x.Notas).MaximumLength(2000);
        RuleFor(x => x.NotaImportante).MaximumLength(120);
        RuleFor(x => x.ContactoPreferido)
            .Must(ClienteContactPreferences.IsValidChannel)
            .WithMessage("Canal preferido inválido. Usa Telefone, WhatsApp, Email ou Sms.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactoPreferido));
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
        RuleFor(x => x.Nif)
            .Must(nif => NifValidator.IsValid(nif))
            .WithMessage("NIF inválido — verifica os 9 dígitos e o check-digit.")
            .When(x => !string.IsNullOrWhiteSpace(x.Nif));
        RuleFor(x => x.Notas).MaximumLength(2000);
        RuleFor(x => x.NotaImportante).MaximumLength(120);
        RuleFor(x => x.ContactoPreferido)
            .Must(ClienteContactPreferences.IsValidChannel)
            .WithMessage("Canal preferido inválido. Usa Telefone, WhatsApp, Email ou Sms.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactoPreferido));
    }
}
