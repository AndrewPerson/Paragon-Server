using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Paragon.Oauth2;

namespace Paragon.Functions
{
    public static class Auth
    {
        [FunctionName("auth")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            ILogger log)
        {
            var body = await req.ReadAsStringAsync();

            if (body.Length == 0) return new BadRequestObjectResult("Body must contain code");

            TokenFactory factory = new TokenFactory();

            if (!factory.FromAuthCode(body, out var token))
                return new BadRequestObjectResult("Invalid code");
            
            var json = await factory.ToJson(token);

            return new OkObjectResult(json);
        }
    }
}
