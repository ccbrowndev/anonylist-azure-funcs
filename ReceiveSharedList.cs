using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Messaging.ServiceBus;
using System.Text;
using System.Collections.Generic;
using System.Web.Http;

namespace anonylist_funcs
{
    public static class ReceiveSharedList
    {
        public record ListDataResponse(string ListId, string ListData, string OriginalSenderId);

        [FunctionName("ReceiveSharedList")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string shareCode = req.Query["shareCode"];
            string senderID = req.Query["senderID"];

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

            string listID = null;
            string originalSenderID = null;
            string listData = null;

            try
            {
                ServiceBusSessionReceiverOptions receiverOptions = new ServiceBusSessionReceiverOptions();
                receiverOptions.ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete;
                var sessionReceiver = await messageClient.AcceptSessionAsync("sharecodes", shareCode, receiverOptions);
                var message = await sessionReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10));

                await sessionReceiver.CloseAsync();

                if (message != null)
                {
                    listID = (string)message.ApplicationProperties.GetValueOrDefault("listID");
                    originalSenderID = (string)message.ApplicationProperties.GetValueOrDefault("senderID");
                    listData = Encoding.UTF8.GetString(message.Body);

                    var confirmationMessage = new ServiceBusMessage(listData);
                    confirmationMessage.ApplicationProperties.Add("senderID", senderID);
                    confirmationMessage.ApplicationProperties.Add("listID", listID);
                    confirmationMessage.ApplicationProperties.Add("recipientID", originalSenderID);

                    await topicSender.SendMessageAsync(confirmationMessage);
                }
                else
                {
                    log.LogWarning("Share code not found!");
                    return new NotFoundResult();
                }
            }
            catch (Exception ex)
            {
                log.LogError("Service Bus Error: " + ex.Message);
                return new InternalServerErrorResult(); 
            }


            log.LogInformation("C# HTTP trigger function processed a request.");

            var responseJson = new ListDataResponse(listID, listData, originalSenderID);
            var response = new OkObjectResult(responseJson);
            response.ContentTypes.Add(new MediaTypeHeaderValue("application/json"));

            return response;
        }
    }
}
