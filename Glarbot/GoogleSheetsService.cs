using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Glarbot
{
    internal class GoogleSheetsService : IGoogleSheetsService
    {
        private readonly INotificationService _notificationService;
        private readonly GoogleSettings _googleSettings;
        private readonly ILogger<GoogleSheetsService> _logger;

        private readonly SheetsService _sheetsService;

        public GoogleSheetsService(INotificationService notificationService,
            IOptions<GoogleSettings> googleSettings,
            ILogger<GoogleSheetsService> logger)
        {
            _notificationService = notificationService;
            _googleSettings = googleSettings.Value;
            _logger = logger;
            _sheetsService = GetSheetsService();
        }

        public async Task<ValueRange> GetAsync(string range, CancellationToken cancellationToken)
        {
            var action = _sheetsService.Spreadsheets.Values.Get(_googleSettings.SpreadsheetId, range);
            return await ExecuteAsync(action, cancellationToken);
        }

        public async Task UpdateAsync(string range, string value, CancellationToken cancellationToken)
        {
            var valueRange = new ValueRange
            {
                Values = [[value]]
            };

            var action = _sheetsService.Spreadsheets.Values
                .Update(valueRange, _googleSettings.SpreadsheetId, range);

            action.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;

            await ExecuteAsync(action, cancellationToken);
        }

        public async Task AppendAsync(IEnumerable<string> values, CancellationToken cancellationToken)
        {
            // deliberate nested cast
            var valueRange = new ValueRange
            {
                Values = [values.Cast<object>().ToList()]
            };

            var action = _sheetsService.Spreadsheets.Values
                .Append(valueRange, _googleSettings.SpreadsheetId, "Data!A1:A1");

            action.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW;

            await ExecuteAsync(action, cancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(SheetsBaseServiceRequest<T> action, CancellationToken cancellationToken = default)
        {
            try
            {
                return await action.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Couldn't get data from Google Sheets.");
                await _notificationService.PushNotification("Glarbot", "Couldn't get data from Google Sheets.", cancellationToken: cancellationToken);
            }

            return default!;
        }

        private SheetsService GetSheetsService()
        {
            var googleCredential = GetCredential(
                _googleSettings.PrivateKeyId,
                _googleSettings.PrivateKey,
                _googleSettings.ClientId
            );

            return new SheetsService(new BaseClientService.Initializer
            {
                ApplicationName = "Glarbot",
                HttpClientInitializer = googleCredential
            });
        }

        private static GoogleCredential GetCredential(string privateKeyId, string privateKey, string clientId)
        {
            var json =
            $@"{{
                ""type"": ""service_account"",
                ""project_id"": ""glarbot"",
                ""private_key_id"": ""{privateKeyId}"",
                ""private_key"": ""{privateKey}"",
                ""client_email"": ""glarbot@glarbot.iam.gserviceaccount.com"",
                ""client_id"": ""{clientId}"",
                ""auth_uri"": ""https://accounts.google.com/o/oauth2/auth"",
                ""token_uri"": ""https://oauth2.googleapis.com/token"",
                ""auth_provider_x509_cert_url"": ""https://www.googleapis.com/oauth2/v1/certs"",
                ""client_x509_cert_url"": ""https://www.googleapis.com/robot/v1/metadata/x509/glarbot%40glarbot.iam.gserviceaccount.com"",
                ""universe_domain"": ""googleapis.com""
            }}";

            return GoogleCredential.FromJson(json)
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }
    }
}
