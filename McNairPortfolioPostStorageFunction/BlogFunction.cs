using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace McNairPortfolioPostStorageFunction
{
    public static class BlogFunction
    {
        private static readonly string storageAccountName = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME");
        private static readonly string storageAccountKey = Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY");
        private static readonly string tableName = Environment.GetEnvironmentVariable("TABLE_NAME");

        private static readonly TableClient tableClient = new TableClient(
            new Uri($"https://{storageAccountName}.table.core.windows.net"),
            tableName,
            new TableSharedKeyCredential(storageAccountName, storageAccountKey)
        );

        [FunctionName("BlogFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (req.Method == HttpMethods.Get)
            {
                // Fetch blog posts
                var queryResults = tableClient.Query<TableEntity>(entity => entity.PartitionKey == "blog")
                                              .OrderByDescending(e => e.GetDateTime("datePosted"))
                                              .Select(entity => new
                                              {
                                                  Title = entity.GetString("title"),
                                                  Text = entity.GetString("text"),
                                                  DatePosted = entity.GetDateTime("datePosted")
                                              });

                return new OkObjectResult(queryResults);
            }
            else if (req.Method == HttpMethods.Post)
            {
                // Create a new blog post
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                string title = data?.title;
                string text = data?.text;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(text))
                {
                    return new BadRequestObjectResult("Please pass a title and text in the request body");
                }

                var newPost = new TableEntity("blog", Guid.NewGuid().ToString())
                {
                    { "title", title },
                    { "text", text },
                    { "datePosted", DateTime.UtcNow }
                };

                try
                {
                    await tableClient.AddEntityAsync(newPost);
                    return new OkObjectResult("Post created successfully");
                }
                catch (RequestFailedException ex)
                {
                    log.LogError($"Error inserting entity: {ex.Message}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }

            return new BadRequestResult(); 
        }
    }
}
