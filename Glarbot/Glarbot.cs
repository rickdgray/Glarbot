using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Web;

namespace Glarbot
{
    internal class Glarbot : BackgroundService
    {
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly Settings _settings;
        private readonly GoogleSettings _googleSettings;
        private readonly ILogger<Glarbot> _logger;

        public Glarbot(IGoogleSheetsService sheetsService,
            IOptions<Settings> settings,
            IOptions<GoogleSettings> googleSettings,
            ILogger<Glarbot> logger)
        {
            _googleSheetsService = sheetsService;
            _settings = settings.Value;
            _googleSettings = googleSettings.Value;
            _logger = logger;
        }

        // as per new httpclient guidelines
        // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
        private static readonly HttpClient _pushoverClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting application.");

            var lastPoll = DateTimeOffset.Now;
            var failCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Pulling Feed.");

                HtmlNodeCollection? nodes = null;

                try
                {
                    var doc = await new HtmlWeb().LoadFromWebAsync(_settings.RedditFeedUrl, cancellationToken);
                    nodes = doc.DocumentNode
                        .SelectSingleNode("//body/div[@class='content' and @role='main']/div[@id='siteTable']")
                        ?.ChildNodes
                        ?? throw new Exception("Couldn't load posts.");
                    failCount = 0;
                }
                catch (HttpRequestException)
                {
                    failCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Couldn't load posts.");
                    failCount++;
                }

                if (failCount == 10)
                {
                    _logger.LogInformation("Reddit not responding.");

                    await PushNotification("Glarbot", "Reddit not responding.", null, cancellationToken);
                }

                if (nodes != null)
                {
                    var posts = ParseHtml(nodes, lastPoll, cancellationToken);

                    // chronological order
                    posts.Reverse();

                    foreach (var post in posts)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (post == null)
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(post.Title))
                        {
                            continue;
                        }

                        await _googleSheetsService.AppendAsync([post.Flair, post.Title, DateTime.UtcNow.ToString(), post.Url], cancellationToken);

                        _logger.LogDebug("Dumped post data; delaying for 3 seconds...");

                        await Task.Delay(3000, cancellationToken);
                    }

                    _logger.LogInformation(
                        "Dumped all posts. Waiting until next poll time: {pollTime}",
                        DateTimeOffset.Now.AddMinutes(_settings.PollRateInMinutes));
                }

                lastPoll = DateTimeOffset.Now;
                await Task.Delay(_settings.PollRateInMinutes * 60000, cancellationToken);
            }
        }

        private List<RedditPost> ParseHtml(HtmlNodeCollection nodes, DateTimeOffset lastPoll, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Parsing data...");

            var posts = new List<RedditPost>();
            foreach (var node in nodes)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return posts;
                }

                // not a post
                if (!node.HasClass("thing"))
                {
                    continue;
                }

                // ignore spacers and ads
                if (node.HasClass("clearleft") || node.HasClass("promoted"))
                {
                    continue;
                }

                if (!long.TryParse(node.GetDataAttribute("timestamp").Value, out long unixTimestamp))
                {
                    continue;
                }

                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp).ToLocalTime();

                _logger.LogDebug("Timestamp: {timestamp}", timestamp);

                if (timestamp < lastPoll)
                {
                    continue;
                }

                var relativePermalink = node.GetDataAttribute("permalink")?.Value ?? string.Empty;

                var postNode = node.SelectSingleNode(".//div[@class='top-matter']");

                var titleNode = postNode.SelectSingleNode("./p[@class='title']");
                var title = titleNode.InnerText.Trim().Split("&#32;").FirstOrDefault() ?? string.Empty;

                var flair = titleNode.FirstChild.GetAttributeValue("title", string.Empty);

                if (title.StartsWith(flair))
                {
                    title = title[flair.Length..];
                }

                var commentsString = postNode.ChildNodes[3].InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(commentsString))
                {
                    commentsString = commentsString.Trim().Split(" comment").FirstOrDefault();
                }

                if (!int.TryParse(commentsString, out int commentCount))
                {
                    continue;
                }

                posts.Add(new RedditPost
                {
                    Flair = flair,
                    Title = HttpUtility.HtmlDecode(title),
                    PostTime = timestamp,
                    Url = $"https://reddit.com{relativePermalink}",
                    CommentCount = commentCount
                });
            }

            _logger.LogInformation("Found {count} new posts.", posts.Count);

            return posts;
        }

        private async Task PushNotification(string title, string message, string? url = default, CancellationToken cancellationToken = default)
        {
            // TODO: customizable notification text
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
            catch (HttpRequestException)
            {
                //TODO: retry logic
            }
            catch (TaskCanceledException ex)
            {
                //TODO: logging these for the time being for debugging
                _logger.LogInformation(ex, "Task Cancelled Exception");
            }

            _logger.LogInformation("Notification pushed.");
        }
    }
}
