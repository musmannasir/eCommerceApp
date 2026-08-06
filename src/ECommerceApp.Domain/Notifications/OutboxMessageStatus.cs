namespace ECommerceApp.Domain.Notifications;

public enum OutboxMessageStatus
{
    Pending,
    Processed,
    Failed,
}
