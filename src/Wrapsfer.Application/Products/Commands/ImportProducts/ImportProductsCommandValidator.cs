using FluentValidation;

namespace Wrapsfer.Application.Products.Commands.ImportProducts;

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage(ProductImportMessages.FileRequired);

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage(ProductImportMessages.UnsupportedFormat);
    }
}
