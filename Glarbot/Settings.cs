using System.ComponentModel.DataAnnotations;

namespace Glarbot
{
    internal class Settings
    {
        [Required] public string PushoverUserKey { get; set; } = string.Empty;
        [Required] public string PushoverAppKey { get; set; } = string.Empty;
        public string RedditFeedUrl { get; set; } = string.Empty;
        public int PollRateInMinutes { get; set; }
    }
}
