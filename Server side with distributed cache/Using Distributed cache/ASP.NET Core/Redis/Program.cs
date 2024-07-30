using WebApplication1.Hubs;
using Microsoft.Azure.SignalR;
using StackExchange.Redis;
using WebApplication1.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

var config = builder.Configuration;
var redisConfig = config.GetSection("ConnectionStrings");
var connectionString = redisConfig["RedisConnectionString"];

//Configure SignalR
builder.Services.AddSignalR().AddStackExchangeRedis(connectionString, options =>
{
    options.Configuration.ChannelPrefix = "docedit";
});


builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = connectionString;
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(connectionString, true);
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IBackgroundTaskQueue>(ctx =>
{
    //Configure maximum queue capacity.
    return new BackgroundTaskQueue(200);
});
builder.Services.AddHostedService<QueuedHostedService>();

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseCors();

app.MapHub<DocumentEditorHub>("/documenteditorhub");

app.MapControllers();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=LogIn}/{userName?}/{id?}");
});

app.Run();
