
using Syncfusion.Collaboration.Core.Extensions;
using Syncfusion.Collaboration.Core.Interfaces;
using WebApplication1.Adapter;


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

builder.Services.AddCollaborationServer(options =>
{
    options.ConnectionString =
        builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
    options.ConnectionType = CollaborationConnectionType.WebSocket;    

});
builder.Services.AddSingleton<ICollaborationAdapter,
DocumentEditorCollaborationAdapter>();


var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseCors();
app.UseWebSockets();
app.MapControllers();
//common collaboration
app.MapCollaborationServer();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=LogIn}/{userName?}/{id?}");
});

app.Run();
