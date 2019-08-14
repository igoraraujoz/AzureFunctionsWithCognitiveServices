using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;
using TextAnalyserFunction.Common;
using Microsoft.Azure.CognitiveServices.ContentModerator;

namespace TextAnalyserFunction
{
    public static class AnalyserFunction
    {
        #region Configs Text Analytics
        private const string ApiKey = "7eb7e48d2b9f49e5a795ef398a3e4278";
        private const string Endpoint = "https://fiaptextanalyser.cognitiveservices.azure.com/";
        #endregion

        #region Configs Content Moderator
        // The base URL fragment for Content Moderator calls.
        private static readonly string AzureBaseURL = "https://fiapcontentmoderator.cognitiveservices.azure.com/";

        // Your Content Moderator subscription key.
        private static readonly string CMSubscriptionKey = "684ce19cf25447ac9b4c1f7bb45edb1a";
        #endregion

        [FunctionName("Analyser")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Request request = JsonConvert.DeserializeObject<Request>(requestBody);

            if (request == null || String.IsNullOrEmpty(request.message))
                return new NoContentResult();

            var credentials = new Common.ApiKeyServiceClientCredentials(ApiKey);
            var textAnalyticsClient = new TextAnalyticsClient(credentials)
            {
                Endpoint = Endpoint
            };
                   
            var response = new Response();
            var result = textAnalyticsClient.DetectLanguage(request.message);

            if (result == null)
                return new BadRequestResult();

            var score = textAnalyticsClient.Sentiment(request.message, result.DetectedLanguages[0].Iso6391Name).Score;

            if (result.DetectedLanguages[0].Iso6391Name != "pt")
            {
                response.Message = "Somente é permitido textos em português!";
                response.Erro = true;
                response.RequestMessage = request.message;

                return new OkObjectResult(response);                
            }

            var moderatorCredentials = new ApiKeyModeratorServiceClienteCredentials(CMSubscriptionKey);

            var moderatorClient = new ContentModeratorClient(moderatorCredentials)
            {
                Endpoint = AzureBaseURL
            };

            string text = request.message;
            text.Replace(System.Environment.NewLine, " ");
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(request.message);
            MemoryStream stream = new MemoryStream(byteArray);

            // Create a Content Moderator client and evaluate the text.
            using (var client = moderatorClient)
            {

                var screenResult = client.TextModeration.ScreenText("text/plain", stream, "eng", true, true, null, true);

                if(screenResult.Classification.ReviewRecommended.HasValue && screenResult.Classification.ReviewRecommended.Value)
                {
                    response.Erro = true;
                    response.Message = "Não utilize palavrões!";
                    response.RequestMessage = request.message;
                    response.Score = score.HasValue ? score.Value : double.NaN;

                    return new OkObjectResult(response);
                }                
            }

            response.Erro = false;
            response.Message = "Executado com sucesso.";
            response.Score = score.HasValue ? score.Value : double.NaN;
            response.RequestMessage = request.message;

            return new OkObjectResult(response);           
        }
    }
}
