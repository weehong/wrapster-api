using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Responses;

namespace Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;

public sealed record GetStaleDraftsReportQuery(DateOnly? From, DateOnly? To)
    : IQuery<IReadOnlyList<WaybillResponse>>;
