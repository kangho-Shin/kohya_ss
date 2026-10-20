using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using OfficePower.Core;

namespace OfficePower.Agent;

public sealed record AgentOptions(string Name, string Token, X509Certificate2 Certificate, int Port, IPAddress BindAddress);

public static class AgentHost
{
    public static WebApplication Build(AgentOptions options, IShutdownExecutor executor)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(AgentHost).Assembly.FullName });
        builder.Host.UseWindowsService(s => s.ServiceName = "OfficePowerAgent");
        builder.Logging.ClearProviders(); // Do not log bearer credentials, certificate keys or pairing data.
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Limits.MaxRequestBodySize = 1024;
            server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            server.Listen(options.BindAddress, options.Port, endpoint => endpoint.UseHttps(https =>
            {
                https.ServerCertificate = options.Certificate;
                https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            }));
        });
        var app = builder.Build();
        var coordinator = new ShutdownCoordinator(executor);
        app.Use(async (context, next) =>
        {
            string authorization = context.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) ||
                !Tokens.Equals(options.Token, authorization[7..]))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.MapGet("/status", () => new AgentStatus(options.Name, coordinator.IsScheduled ? "shutdown-scheduled" : "online"));
        app.MapPost("/shutdown", async (HttpContext context) =>
        {
            try
            {
                // Once accepted, client disconnection must not silently cancel an OS request.
                bool scheduled = await coordinator.ScheduleAsync(CancellationToken.None);
                return scheduled ? Results.Json(new ShutdownReply("scheduled", 30), statusCode: 202) : Results.Conflict();
            }
            catch (Exception)
            {
                return Results.Problem("Windows 종료 예약에 실패했습니다. 서비스 계정과 권한을 확인하세요.", statusCode: 503);
            }
        });
        return app;
    }
}
