using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Paragon.Oauth2
{
    public class TokenFactory
    {
        public bool FromAuthCode(string authCode, out Token token)
        {
            string tokenJson;

            using var tokenResponse = HttpClientFactory.Create().PostAsync("https://student.sbhs.net.au/api/token", new FormUrlEncodedContent
            (
                new Dictionary<string, string>
                {
                    {"code", authCode},
                    {"grant_type", "authorization_code"},
                    {"client_id", GetID()},
                    {"client_secret", GetSecret()},
                    {"redirect_uri", GetRedirect()}
                }
            )).Result;
            {
                tokenJson = tokenResponse.Content.ReadAsStringAsync().Result;
            }

            return FromJson(tokenJson, out token);
        }

        public bool FromJson(string json, out Token token, string refreshToken = null)
        {
            token = new Token();

            using var document = JsonDocument.Parse(json);
            {
                var root = document.RootElement;

                if (root.TryGetProperty("error", out _)) return false;

                if (root.TryGetProperty("access_token", out var accessTokenElement))
                    token.AccessToken = accessTokenElement.GetString();
                else
                    return false;

                if (root.TryGetProperty("refresh_token", out var refreshTokenElement))
                    token.RefreshToken = refreshTokenElement.GetString();
                else if (refreshToken == null)
                    return false;
                else
                    token.RefreshToken = refreshToken;

                if (root.TryGetProperty("expiry", out var expiryElement))
                    token.Expiry = expiryElement.GetDateTime();
                else
                    token.Expiry = DateTime.Now.AddHours(1);

                if (root.TryGetProperty("termination", out var terminationElement))
                    token.Termination = terminationElement.GetDateTime();
                else
                    token.Termination = DateTime.Now.AddDays(90);
            }

            return true;
        }

        public bool Refresh(ref Token token)
        {
            string tokenJson;
            using var tokenResponse = HttpClientFactory.Create().PostAsync("https://student.sbhs.net.au/api/token", new FormUrlEncodedContent
            (
                new Dictionary<string, string>
                {
                    {"refresh_token", token.RefreshToken},
                    {"grant_type", "refresh_token"},
                    {"client_id", GetID()},
                    {"client_secret", GetSecret()}
                }
            )).Result;
            {
                tokenJson = tokenResponse.Content.ReadAsStringAsync().Result;
            }

            return FromJson(tokenJson, out token, token.RefreshToken);
        }

        public async Task<string> ToJson(Token token)
        {
            string json;

            using MemoryStream jsonStream = new MemoryStream();
            {
                using Utf8JsonWriter jsonWriter = new Utf8JsonWriter(jsonStream);
                {
                    jsonWriter.WriteStartObject();

                    jsonWriter.WriteString("access_token", token.AccessToken);
                    jsonWriter.WriteString("refresh_token", token.RefreshToken);
                    jsonWriter.WriteString("expiry", token.Expiry.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF"));
                    jsonWriter.WriteString("termination", token.Termination.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF"));

                    jsonWriter.WriteEndObject();

                    await jsonWriter.FlushAsync();

                    json = Encoding.UTF8.GetString(jsonStream.ToArray());
                }
            }

            return json;
        }

        private string GetID() => Environment.GetEnvironmentVariable("ID");
        private string GetSecret() => Environment.GetEnvironmentVariable("SECRET");
        private string GetRedirect() => Environment.GetEnvironmentVariable("REDIRECT");
    }

    public struct Token
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiry { get; set; }
        // Say the token is expired if it expires in 5 minutes or less.
        // This is to avoid the token potentially expiry while fetching resources.
        public bool Expired => DateTime.Now.AddMinutes(5) > Expiry;
        public DateTime Termination { get; set; }
        // Say the token is terminated if it terminates in 5 minutes or less.
        // Same reason as for when it Expires.
        public bool Terminated => DateTime.Now.AddMinutes(5) > Termination;
    }
}