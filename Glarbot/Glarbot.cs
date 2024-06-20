using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Web;

namespace Glarbot
{
    internal class Glarbot : BackgroundService
    {
        private readonly INotificationService _notificationService;
        private readonly IGoogleSheetsService _googleSheetsService;
        private readonly Settings _settings;
        private readonly ILogger<Glarbot> _logger;

        public Glarbot(INotificationService notificationService,
            IGoogleSheetsService sheetsService,
            IOptions<Settings> settings,
            ILogger<Glarbot> logger)
        {
            _notificationService = notificationService;
            _googleSheetsService = sheetsService;
            _settings = settings.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting application.");
            await _notificationService.PushNotification("Glarbot", "Starting application.", cancellationToken: cancellationToken);

            var lastPoll = DateTimeOffset.Now;
            var failCount = 0;

            await _googleSheetsService.UpdateAsync("Data!L2:L2", DateTime.UtcNow.ToString(), cancellationToken);

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

                    await _notificationService.PushNotification("Glarbot", "Reddit not responding.", null, cancellationToken);
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
                        "Waiting until next poll time: {pollTime}",
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
    }
}
