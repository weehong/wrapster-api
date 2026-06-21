using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Waybills.Commands.RestoreAutoCancelledDraft;

public sealed record RestoreAutoCancelledDraftCommand(Guid Id) : ICommand;
