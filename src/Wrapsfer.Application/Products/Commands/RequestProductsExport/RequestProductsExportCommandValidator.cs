using FluentValidation;
using Wrapsfer.Application.Products.Commands.ImportProducts;

namespace Wrapsfer.Application.Products.Commands.RequestProductsExport;

public sealed class RequestProductsExportCommandValidator : AbstractValidator<RequestProductsExportCommand>
{
    public RequestProductsExportCommandValidator() =>
        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage(ProductImportMessages.UnsupportedFormat);
}
