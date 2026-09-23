using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using vkc_apinotification.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<kalanjaliDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("adminconnection1")));
//builder.Services.AddDbContext<kalanjaliDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("adminconnection1"),
//        x => x.CommandTimeout(180)));

//inject message controller
builder.Services.AddScoped<MessageController>();

//validation
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

//firebase
FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kalanjali-f1149-firebase-adminsdk-fbsvc-42c5b5fc50.json")),
});
////


var app = builder.Build();


//app.UseWhen(context =>
//{
//    var path = context.Request.Path;
//    return path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/login/Login") && !path.StartsWithSegments("/api/login/sign_up") && !path.StartsWithSegments("/api/notification/view_notification") && !path.StartsWithSegments("/api/notification/send_notification") && !path.StartsWithSegments("/api/Message/SendMessageAsync") && !path.StartsWithSegments("/api/institute/view_institute") && !path.StartsWithSegments("/api/class/get_class") && !path.StartsWithSegments("/api/division/get_division") && !path.StartsWithSegments("/api/dashboard/dashboard_web") && !path.StartsWithSegments("/api/dashboard/dashboard_institute") && !path.StartsWithSegments("/api/dashboard/student_prgm_details") && !path.StartsWithSegments("/api/dashboard/dashboard_result_published") && !path.StartsWithSegments("/api/dashboard/institute_points") && !path.StartsWithSegments("/api/program/view_program") && !path.StartsWithSegments("/api/student/get_student") &&  !path.StartsWithSegments("/api/appversion/Getappversionmodel");
//}, appBuilder =>
//{
//    appBuilder.UseMiddleware<ApiKeyMiddleware>();
//});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});
app.UseAuthorization();

app.MapControllers();

app.Run();
