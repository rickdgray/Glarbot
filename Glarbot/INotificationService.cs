
namespace Glarbot
{
    internal interface INotificationService
    {
        Task PushNotification(string title, string message, string? url = null, CancellationToken cancellationToken = default);
    }
}