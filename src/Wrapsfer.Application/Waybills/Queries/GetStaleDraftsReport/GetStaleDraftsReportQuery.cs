using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Responses;

namespace Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;

public sealed record GetStaleDraftsReportQuery(int HoursBack = 48) : IQuery<IReadOnlyList<WaybillResponse>>;
