using kalanjali_api.Data;
using Newtonsoft.Json;

namespace kalanjali_api.Model
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly kalanjaliDbContext _context;
        private
        const string APIKEY = "XApiKey";
        public ApiKeyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        //public async Task InvokeAsync(HttpContext context)
        //{
        //    if (!context.Request.Headers.TryGetValue(APIKEY, out
        //            var extractedApiKey))
        //    {
        //        context.Response.StatusCode = 401;
        //        await context.Response.WriteAsync("Api Key was not provided ");
        //        return;
        //    }
        //    var appSettings = context.RequestServices.GetRequiredService<IConfiguration>();
        //    var apiKey = appSettings.GetValue<string>("ApiSettings:XApiKey");
        //    if (!apiKey.Equals(extractedApiKey))
        //    {
        //        context.Response.StatusCode = 401;
        //        await context.Response.WriteAsync("Unauthorized client");
        //        return;
        //    }
        //    await _next(context);
        //}



        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            //if (!context.Request.Headers.TryGetValue(APIKEY, out var extractedApiKey))
            //{
            //    context.Response.StatusCode = 401;
            //    await context.Response.WriteAsync("Api Key was not provided.");
            //    return;
            //}
            if (!context.Request.Headers.TryGetValue(APIKEY, out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                context.Response.Headers.Remove("WWW-Authenticate"); 
                await context.Response.WriteAsync("Api Key was not provided.");
                return;
            }


            using (var scope = serviceProvider.CreateScope())
            {
                var kvnDbContext = scope.ServiceProvider.GetRequiredService<kalanjaliDbContext>();

                var apiKey = kvnDbContext.tbl_user.Where(x => x.enc_key == "" + extractedApiKey).ToList();//.SingleOrDefault(a => a.token == extractedApiKey);

                //if (apiKey.Count == 0)
                //{
                //    context.Response.StatusCode = 401; // Unauthorized
                //    await context.Response.WriteAsync("Unauthorized client.");
                //    return;
                //}
                if (apiKey.Count == 0)
                {
                    //context.Response.StatusCode = 401; // Unauthorized
                    //context.Response.Headers.Remove("WWW-Authenticate"); // 🔑 REMOVE THIS HEADER
                    //await context.Response.WriteAsync("Unauthorized client.");
                    //return;
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers.Remove("WWW-Authenticate");
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        status = false,
                        message = "Unauthorized client."
                    }));

                }

                else
                {


                    await _next(context);

                }

            }



            //  else
            //{


            //    var tokenExpirationTime = apiKey1.token_date.Value.AddMinutes(15);

            //    // Check if the current time is within the valid time frame
            //    var currentTime = DateTime.Now;

            //    if (currentTime < tokenExpirationTime)
            //    {

            //        apiKey1.token_date = DateTime.Now;


            //        kvnDbContext.SaveChanges();



            //        // Token is valid, continue the request
            //        await _next(context);
            //    }
            //    else
            //    {
            //        context.Response.StatusCode = 401; // Unauthorized
            //        await context.Response.WriteAsync("Unauthorized client.");
            //        return;
            //    }

            //    //await _next(context);

            //}




            //apiresponse1 apiresponse = JsonConvert.DeserializeObject<apiresponse1>(result);



            //var appSettings = context.RequestServices.GetRequiredService<IConfiguration>();
            //var apiKey = appSettings.GetValue<string>("ApiSettings:XApiKey");

            //if (apiKey == null)
            //{
            //    // Log or throw an exception to indicate that the API key was not found in configuration
            //    context.Response.StatusCode = 500; // Internal Server Error
            //    await context.Response.WriteAsync("API Key not found in configuration.");
            //    return;
            //}



            //LoginController web = new LoginController();
            //if (web.token_check(extractedApiKey))
            //{
            //    context.Response.StatusCode = 401;
            //    await context.Response.WriteAsync("Unauthorized client.");
            //    return;
            //}


        }


    }
}
