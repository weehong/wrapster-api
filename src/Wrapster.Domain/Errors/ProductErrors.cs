using Wrapster.Domain.Common;

namespace Wrapster.Domain.Errors;

public static class ProductErrors
{
    public static readonly Error NotFound = new(
        "Product.NotFound",
        "The product was not found",
        ErrorType.NotFound);

    public static readonly Error BarcodeAlreadyExists = new(
        "Product.BarcodeAlreadyExists",
        "A product with this barcode already exists",
        ErrorType.Conflict);

    public static readonly Error SkuAlreadyExists = new(
        "Product.SkuAlreadyExists",
        "A product with this SKU code already exists",
        ErrorType.Conflict);

    public static readonly Error InsufficientStock = new(
        "Product.InsufficientStock",
        "Insufficient stock to complete the operation",
        ErrorType.Validation);

    public static readonly Error CannotDeductBundleStock = new(
        "Product.CannotDeductBundleStock",
        "Cannot directly deduct stock from a bundle product",
        ErrorType.Validation);

    public static readonly Error InvalidUnpackQuantity = new(
        "Product.InvalidUnpackQuantity",
        "The unpack quantity is invalid",
        ErrorType.Validation);

    public static readonly Error NotABundle = new(
        "Product.NotABundle",
        "The product is not a bundle",
        ErrorType.Validation);

    public static readonly Error ComponentNotFound = new(
        "Product.ComponentNotFound",
        "The product component was not found",
        ErrorType.NotFound);

    public static readonly Error NotAPackage = new(
        "Product.NotAPackage",
        "The product is not a package",
        ErrorType.Validation);

    public static readonly Error MissingUnpackTarget = new(
        "Product.MissingUnpackTarget",
        "A package product must specify an unpack target product and quantity",
        ErrorType.Validation);

    public static readonly Error InvalidUnpackTarget = new(
        "Product.InvalidUnpackTarget",
        "The unpack target product is invalid",
        ErrorType.Validation);

    public static readonly Error InvalidComponentType = new(
        "Product.InvalidComponentType",
        "Only single products can be used as bundle components",
        ErrorType.Validation);

    public static readonly Error CannotSetBundleStock = new(
        "Product.CannotSetBundleStock",
        "Cannot directly set stock on a bundle product",
        ErrorType.Validation);

    public static readonly Error InvalidTenantId = new(
        "Product.InvalidTenantId",
        "Tenant ID is required",
        ErrorType.Validation);

    public static readonly Error InvalidBarcode = new(
        "Product.InvalidBarcode",
        "Barcode is required",
        ErrorType.Validation);

    public static readonly Error InvalidName = new(
        "Product.InvalidName",
        "Name is required",
        ErrorType.Validation);

    public static readonly Error InvalidCost = new(
        "Product.InvalidCost",
        "Cost must be greater than or equal to zero",
        ErrorType.Validation);

    public static readonly Error InvalidStockQuantity = new(
        "Product.InvalidStockQuantity",
        "Stock quantity must be greater than or equal to zero",
        ErrorType.Validation);

    public static readonly Error InvalidRestoreAmount = new(
        "Product.InvalidRestoreAmount",
        "Restore amount must be greater than zero",
        ErrorType.Validation);

    public static readonly Error SelfReferencingUnpackTarget = new(
        "Product.SelfReferencingUnpackTarget",
        "A product cannot reference itself as its own unpack target",
        ErrorType.Validation);

    public static readonly Error NotAPackageForUnpackConfig = new(
        "Product.NotAPackageForUnpackConfig",
        "Unpack configuration can only be set on package products",
        ErrorType.Validation);

    public static readonly Error DuplicateComponentChildId = new(
        "Product.DuplicateComponentChildId",
        "A bundle cannot contain duplicate component products",
        ErrorType.Validation);

    public static readonly Error ProductReferencedAsUnpackTarget = new(
        "Product.ProductReferencedAsUnpackTarget",
        "Cannot delete a product that is referenced as an unpack target by another product",
        ErrorType.Validation);

    public static readonly Error InvalidComponentQuantity = new(
        "ProductComponent.InvalidQuantity",
        "Component quantity must be greater than zero",
        ErrorType.Validation);

    public static readonly Error InvalidComponentParentId = new(
        "ProductComponent.InvalidParentId",
        "Parent product ID must not be empty",
        ErrorType.Validation);

    public static readonly Error InvalidComponentChildId = new(
        "ProductComponent.InvalidChildId",
        "Child product ID must not be empty",
        ErrorType.Validation);

    public static readonly Error SelfReferencingComponent = new(
        "ProductComponent.SelfReferencing",
        "A product cannot be a component of itself",
        ErrorType.Validation);

    public static readonly Error InvalidComponentTenantId = new(
        "ProductComponent.InvalidTenantId",
        "Tenant ID is required for product component",
        ErrorType.Validation);
}
