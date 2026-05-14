using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string Realm,
    string UserId,
    string Username,
    string CurrentPassword,
    string NewPassword) : ICommand;
