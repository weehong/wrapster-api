using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Auth.Responses;

namespace Wrapsfer.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Realm,
    string Username,
    [property: SensitiveData] string Password) : ICommand<LoginResponse>;
