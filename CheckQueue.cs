using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Messaging.ServiceBus;
using System.Collections.Generic;
using System.Text;
using System.Web.Http;
using Microsoft.Net.Http.Headers;

namespace anonylist_funcs
{
    public class CheckQueue
    {
        public record ListDataObject(string ListId, string ListData, string OriginalSenderId);
        public record ListUpdatesResponse(List<ListDataObject> ListUpdates);

        [FunctionName("CheckQueue")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string userID = req.Query["userID"];
            log.LogInformation(userID);

            string connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var queueClient = new ServiceBusClient(connectionString);

            var receiverOptions = new ServiceBusReceiverOptions();
            receiverOptions.ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete;
            var receiver = queueClient.CreateReceiver(userID, receiverOptions);
            ListUpdatesResponse receivedUpdates = new ListUpdatesResponse(new List<ListDataObject>());

            try
            {
                log.LogInformation("Entering receiver loop");
                var updateList = await receiver.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(10));
                foreach (var update in updateList)
                {
                    log.LogInformation("Processing list update " + update.ApplicationProperties["listID"]);
                    receivedUpdates.ListUpdates.Add(processUpdateMessage(update));
                }
                await receiver.CloseAsync();
                log.LogInformation("Receiver loop complete");
            }
            catch (Exception ex)
            {
                log.LogError("Error checking queue for ID " + userID + ": " + ex);
                return new InternalServerErrorResult();
            }

            log.LogInformation("C# HTTP trigger function processed a request.");

            OkObjectResult response = new OkObjectResult(receivedUpdates);
            response.ContentTypes.Add(new MediaTypeHeaderValue("application/json"));

            return response;
        }

        private static ListDataObject processUpdateMessage(ServiceBusReceivedMessage message)
        {
            Console.WriteLine("Entering processUpdateMessage function");
            if (message == null) return null;

            string senderID = message.ApplicationProperties.GetValueOrDefault("senderID").ToString();
            string listID = message.ApplicationProperties.GetValueOrDefault("listID").ToString();
            string listData = Encoding.UTF8.GetString(message.Body);

            return new ListDataObject(listID, listData, senderID);
        }
    }
}
