using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Api.Contracts;

public sealed record RequestFulfillmentDelegationRequest(FulfillmentShippingMethod DefaultShippingMethod);
