using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.Auth.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string Realm,
    string UserId,
    string Username,
    [property: SensitiveData] string CurrentPassword,
    [property: SensitiveData] string NewPassword) : ICommand;
