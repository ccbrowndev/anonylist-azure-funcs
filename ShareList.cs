using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Net.Http.Headers;

namespace anonylist_funcs
{
    public static class ShareList
    {
        [FunctionName("ShareList")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string shareCode = req.Query["shareCode"];
            string senderID = req.Query["senderID"];
            string listID = req.Query["listID"];
            string listData = req.Query["listData"];

            string connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");

            var messageClient = new ServiceBusClient(connectionString);
            var topicSender = messageClient.CreateSender("lists");

            var adminClient = new ServiceBusAdministrationClient(connectionString);

            if (!await adminClient.QueueExistsAsync(senderID))
            {
                try
                {
                    await adminClient.CreateQueueAsync(senderID);
                    var subOptions = new CreateSubscriptionOptions("lists", senderID)
                    {
                        ForwardTo = senderID
                    };
                    var ruleOptions = new CreateRuleOptions("subToOwnIDRule", new SqlRuleFilter("recipientID = '" + senderID + "'"));
                    await adminClient.CreateSubscriptionAsync(subOptions, ruleOptions);

                }
                catch (Exception ex)
                {
                    log.LogError("Service Bus Error: " + ex.Message);
                }
            }

            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(listData))
            {
                Subject = "shareCode",
                SessionId = shareCode
            };
            message.ApplicationProperties.Add("senderID", senderID);
            message.ApplicationProperties.Add("listID", listID);

            try
            {
                await topicSender.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                log.LogError("Service Bus Error: " + ex.Message);
            }

            log.LogInformation("C# HTTP trigger function processed a request.");

            OkObjectResult response = new OkObjectResult("List successfully placed on share code queue.");
            response.ContentTypes.Add(new MediaTypeHeaderValue("text/plain"));

            return response;
        }
    }
}
