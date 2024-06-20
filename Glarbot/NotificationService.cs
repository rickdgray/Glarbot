using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glarbot
{
    internal class NotificationService : INotificationService
    {
        private readonly Settings _settings;
        private readonly ILogger<NotificationService> _logger;

        // as per new httpclient guidelines
        // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
        private static readonly HttpClient _pushoverClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        public NotificationService(IOptions<Settings> settings,
            ILogger<NotificationService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task PushNotification(string title, string message, string? url = default, CancellationToken cancellationToken = default)
        {
            var notification = new Dictionary<string, string>
            {
                { "token", _settings.PushoverAppKey },
                { "user", _settings.PushoverUserKey },
                { "title", title },
                { "message", message }
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                notification.Add("url", url);
                notification.Add("url_title", "Read more");
            }

            try
            {
                await _pushoverClient.PostAsync(
                    "https://api.pushover.net/1/messages.json",
                    new FormUrlEncodedContent(notification),
                    cancellationToken);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "Task Cancelled Exception");
            }

            _logger.LogInformation("Notification pushed.");
        }
    }
}
