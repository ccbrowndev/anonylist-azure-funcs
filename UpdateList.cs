using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using System.Text;
using System.Web.Http;
using Microsoft.Net.Http.Headers;

namespace anonylist_funcs
{
    public static class UpdateList
    {
        [FunctionName("UpdateList")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string senderID = req.Query["senderID"];
            string recipientString = req.Query["recipientIDList"];
            string[] recipients = recipientString.Split(",");
            string listID = req.Query["listID"];
            string listData = req.Query["listData"];

            string connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var messageClient = new ServiceBusClient(connectionString);

            var topicSender = messageClient.CreateSender("lists");

            try
            {
                for (int i = 0; i < recipients.Length; i++)
                {
                    ServiceBusMessage message = new ServiceBusMessage(Encoding.UTF8.GetBytes(listData));
                    message.ApplicationProperties.Add("listID", listID);
                    message.ApplicationProperties.Add("senderID", senderID);
                    message.ApplicationProperties.Add("recipientID", recipients[i]);

                    topicSender.SendMessageAsync(message);
                }

            }
            catch (Exception ex)
            {
                log.LogError("Error sending list update: " + ex.Message);
                return new InternalServerErrorResult();
            }

            log.LogInformation("C# HTTP trigger function processed a request.");

            OkObjectResult response = new OkObjectResult("Successfully updated list " + listID + " for " + recipients.Length + " users.");
            response.ContentTypes.Add(new MediaTypeHeaderValue("text/plain"));

            return response;
        }
    }
}
