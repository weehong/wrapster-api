namespace Wrapsfer.Application.Products.Services;

public enum LowStockAlertOutcome
{
    Sent = 1,
    NoRecipients = 2,
    Suppressed = 3,
    MaxReached = 4
}
