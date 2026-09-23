using FirebaseAdmin.Messaging;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Mvc;

namespace vkc_apinotification.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly ILogger<MessageController> _logger;

        public MessageController(ILogger<MessageController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        [Route("SendMessageAsync")]

        public async Task<IActionResult> SendMessageAsync([FromBody] MessageRequest request)
        {
            try
            {

                string aa1 = @"'{
        ""id"": 0,
        ""channelKey"": ""basic_channel"",
        ""title"": ""title"",
        ""description"":""description"",
        ""body"": ""body"",
        ""autoDismissible"": true,
        ""payload"": {
          ""type_of_msg"": ""admin topic"",
          ""message"": ""description"",
        }}'";

                string aa = "{"
                    + "\"id\":100,"
                    + "\"channelKey\":\"basic_channel\","
                    + "\"title\":\"" + request.Title + "\","
                    + "\"description\":\"" + request.Body + "\","
                    + "\"body\":\"" + request.Body + "\","
                    + "\"autoDismissible\":true,"
    + "\"content\":\"{\\\"payload\\\":{\\\"type\\\":\\\"notification\\\"}}\""

                    + "}";

                //aa = aa.Replace("'", "\"");

                Notification notification = new Notification
                {
                    Title = request.Title,
                    Body = request.Body
                };


                var message = new Message
                {
                    Android = new AndroidConfig
                    {
                        Priority = Priority.High
                    },
                    Apns = new ApnsConfig
                    {
                        Headers = new Dictionary<string, string>
        {
            { "apns-priority", "10" }, // high priority for iOS
            { "content-available", "1" }
        }
                    },
                    Data = new Dictionary<string, string>
    {
        { "title", request.Title },
        { "body", request.Body },
       { "content", ""+aa }
    }
                };


                // Set either Token or Topic based on the request
                if (!string.IsNullOrEmpty(request.DeviceToken))
                {
                    message.Token = request.DeviceToken;
                }
                else if (!string.IsNullOrEmpty(request.Topic))
                {
                    message.Topic = request.Topic;
                }
                else
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Failed to send message.",
                        error = "Either 'DeviceToken' or 'Topic' must be provided."
                    });
                }

                var messaging = FirebaseMessaging.DefaultInstance;
                var result = await messaging.SendAsync(message);

                // Return a successful JSON response
                return Ok(new
                {
                    status = true,
                    message = "Message sent successfully.",
                    resultId = result
                });
            }
            catch (Exception ex)
            {
                // Return a failure JSON response
                return BadRequest(new
                {
                    status = false,
                    message = "Failed to send message.",
                    error = ex.Message
                });
            }
        }


    }
}