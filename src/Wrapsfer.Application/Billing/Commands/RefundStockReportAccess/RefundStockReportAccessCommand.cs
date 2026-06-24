using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Billing.Commands.RefundStockReportAccess;

public sealed record RefundStockReportAccessCommand(string TenantId) : ICommand;
