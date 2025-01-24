using Microsoft.AspNetCore.RateLimiting;
using Server.Auxiliary;
using Server.ClientNotifications;
using Server.Options;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddJsonFile("appsettings.json");

            builder.Services.AddOptions<MessagesSettings>()
                .Bind(builder.Configuration.GetSection(nameof(MessagesSettings)))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<ClientNotificationServiceSettings>()
                .Bind(builder.Configuration.GetSection(nameof(ClientNotificationServiceSettings)))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<WorkerSettings>()
                .Bind(builder.Configuration.GetSection(nameof(WorkerSettings)))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddTransient<ISemaphore, SingleRequestSemaphoreSlimWrapper>();

            builder.Services.AddSingleton<IClientNotificationService, ClientNotificationService>();

            builder.Services.AddScoped<IWorker, Worker>();
            builder.Services.AddScoped<IGuidProvider, GuidProvider>();

            builder.Services.AddRateLimiter(
                rl => {
                    rl.AddConcurrencyLimiter(
                        policyName: RateLimiterPolicyNames.MessagesRateLimiter,
                        options =>
                        {
                            options.PermitLimit = 1;
                            options.QueueLimit = 0;
                        }
                        );
                }
                );

            builder.Services.AddRateLimiter(
                rl => {
                    rl.AddConcurrencyLimiter(
                        policyName: RateLimiterPolicyNames.WorkStartRateLimiter,
                        options =>
                        {
                            options.PermitLimit = 1;
                            options.QueueLimit = 0;
                        }
                        );
                }
                );

            builder.Logging.AddConsole();

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseAuthorization();

            app.MapControllers();

            app.UseWebSockets();

            app.UseRateLimiter();

            app.Run();
        }
    }
}
