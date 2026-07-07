namespace Wrapsfer.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public void SetCreatedAt(DateTime createdAt) => CreatedAt = createdAt;
    public void SetUpdatedAt(DateTime updatedAt) => UpdatedAt = updatedAt;
    public void SetCreatedBy(string createdBy) => CreatedBy = createdBy;
    public void SetUpdatedBy(string updatedBy) => UpdatedBy = updatedBy;
}
