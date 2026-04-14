using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Responses;

namespace Wrapster.Application.Waybills.Queries.GetStaleDraftsReport;

public sealed record GetStaleDraftsReportQuery(int HoursBack = 48) : IQuery<IReadOnlyList<WaybillResponse>>;
