﻿namespace Glarbot
{
    internal static class CredentialHelper
    {
        public static string GetGoogleCredential(string privateKeyId, string privateKey, string clientId)
        {
            return
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
        }
    }
}
