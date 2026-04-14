namespace Wrapsfer.Domain.Common;

public interface IValidationResult<out TSelf> where TSelf : IValidationResult<TSelf>
{
    static abstract TSelf Failure(Error error);
}
