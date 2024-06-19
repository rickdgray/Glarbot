using System.ComponentModel.DataAnnotations;

namespace Glarbot
{
    internal class GoogleSettings
    {
        [Required] public string PrivateKeyId { get; set; } = string.Empty;
        [Required] public string PrivateKey { get; set; } = string.Empty;
        [Required] public string ClientId { get; set; } = string.Empty;
        [Required] public string SpreadsheetId { get; set; } = string.Empty;
    }
}
