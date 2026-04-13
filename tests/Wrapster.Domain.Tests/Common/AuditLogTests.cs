using Wrapster.Domain.Common;

namespace Wrapster.Domain.Tests.Common;

public class AuditLogTests
{
    [Fact]
    public void AuditLog_CreatesWithGeneratedId()
    {
        AuditLog log = new()
        {
            EntityName = "Product",
            EntityId = "123",
            Action = AuditAction.Created,
            Changes = "{\"Name\":\"Widget\"}",
            UserId = "user-1",
            TenantId = "tenant-1",
            Timestamp = DateTime.UtcNow
        };

        log.Id.Should().NotBeEmpty();
        log.EntityName.Should().Be("Product");
        log.EntityId.Should().Be("123");
        log.Action.Should().Be(AuditAction.Created);
        log.Changes.Should().Contain("Widget");
    }
}
