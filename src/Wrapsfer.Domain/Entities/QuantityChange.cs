namespace Wrapsfer.Domain.Entities;

public readonly record struct QuantityChange(Guid ProductId, int Delta);
