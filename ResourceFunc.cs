using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Paragon.Oauth2;

namespace Paragon.Functions
{
    public static class Resource
    {
        private static readonly Dictionary<string, string> Resources = new Dictionary<string, string>()
        {
            { "announcements", "dailynews/list.json" },
            { "calendar", "diarycalendar/events.json" },
            { "dailytimetable", "timetable/daytimetable.json" },
            { "timetable", "timetable/timetable.json" },
            { "userinfo", "details/userinfo.json" }
        };

        [FunctionName("resource")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req,
            ILogger log)
        {
            if (!req.Query.TryGetValue("resource", out var resourceString))
                return new BadRequestObjectResult("Query must contain resource");

            if (!req.Query.TryGetValue("token", out var tokenJson))
                return new BadRequestObjectResult("Query must contain token");

            string resource;

            if (resourceString == "all") resource = "ALL";
            else
            {
                if (!Resources.TryGetValue(resourceString, out resource))
                    return new BadRequestObjectResult("Resource specified is not valid");
            }
            
            var factory = new TokenFactory();
            var validToken = factory.FromJson(tokenJson, out Token token);

            if (!validToken) return new BadRequestObjectResult("Invalid token");

            if (token.Terminated) return new UnprocessableEntityObjectResult("Token is terminated");

            if (token.Expired)
            {
                if (!factory.Refresh(ref token))
                    return new BadRequestObjectResult("Invalid refresh code in token");
            }

            if (resource == "ALL")
            {
                StringBuilder jsonBuilder = new StringBuilder();

                jsonBuilder.Append('{');

                foreach (var sbhsResource in Resources)
                {
                    if (TryGetResource(sbhsResource.Value, token.AccessToken, out var json))
                    {
                        jsonBuilder.Append($@"""{sbhsResource.Key}"":{json},");
                    }
                    else return new UnauthorizedResult();
                }

                jsonBuilder[^1] = '}';

                GC.Collect();

                return new OkObjectResult(@$"{{""result"":{jsonBuilder},""token"":{await factory.ToJson(token)}}}");
            }
            else
            {
                if (!TryGetResource(resource, token.AccessToken, out var resourceJson))
                    return new UnauthorizedResult();

                GC.Collect();

                return new OkObjectResult(@$"{{""result"":{resourceJson},""token"":{await factory.ToJson(token)}}}");
            }
                
        }

        private static bool TryGetResource(string resource, string accessToken, out string resourceResult, ILogger log = null)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "https://student.sbhs.net.au/api/" + resource);
            message.Headers.Add("Authorization", new string[] { "Bearer " + accessToken });

            bool successful;
            using var response = HttpClientFactory.Create().SendAsync(message).Result;
            {
                resourceResult = response.Content.ReadAsStringAsync().Result;
                successful = response.IsSuccessStatusCode;
            }

            return successful;
        }
    }
}