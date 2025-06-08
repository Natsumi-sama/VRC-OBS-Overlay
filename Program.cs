using System.Text;
using VRC_OBS_Overlay.Components;
using MudBlazor.Services;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using ILogger = Serilog.ILogger;

internal static class Program
{
    private static ILogger Logger;
    private static event Action<WorldInfoData>? UpdateUi;
    private static WorldInfoData? CurrentWorldInfoData;
    
    public static void RegisterUpdateUi(Action<WorldInfoData> action)
    {
        UpdateUi += action;
        if (CurrentWorldInfoData != null)
            action(CurrentWorldInfoData);
    }
    
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "VRC OBS Overlay";
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();
        Logger = Log.ForContext("SourceContext", "Core");
        
        var updateLoop = new Thread(UpdateLoop);
        updateLoop.Start();

        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseStaticWebAssets();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddSerilog((services, lc) => lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new ExpressionTemplate(
                // Include trace and span ids when present.
                "[{@t:HH:mm:ss} {@l:u3} {Coalesce(Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),'<none>')}] {@m}\n{@x}",
                theme: TemplateTheme.Literate)));

        builder.Services.AddMudServices();

        var app = builder.Build();
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            await next.Invoke();
        });
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
    
    private static void UpdateLoop()
    {
        while (true)
        {
            Thread.Sleep(2000);
            var regData = PollRegistry.GetVRChatLocation();
            if (regData == null)
            {
                if (!string.IsNullOrEmpty(CurrentWorldInfoData?.WorldId))
                {
                    Logger.Information("No VRChat world info found, clearing current world info");
                    CurrentWorldInfoData = new WorldInfoData
                    {
                        WorldId = string.Empty,
                        WorldName = string.Empty,
                        AuthorName = string.Empty,
                        ImageUrl = string.Empty
                    };
                    UpdateUi?.Invoke(CurrentWorldInfoData);
                }
                continue;
            }
            if (CurrentWorldInfoData?.WorldId == regData.WorldId)
                continue;

            Logger.Information("Found new VRChat world info: {WorldId} - {WorldName}", regData.WorldId, regData.WorldName);
            var worldInfo = WorldInfo.GetWorldInfo(regData.WorldId).Result;
            if (worldInfo == null)
            {
                Logger.Warning("Failed to get world info for ID:{WorldId}, Name:{WorldName}", regData.WorldId, regData.WorldName);
                CurrentWorldInfoData = new WorldInfoData
                {
                    WorldId = regData.WorldId,
                    WorldName = regData.WorldName,
                    AuthorName = string.Empty,
                    ImageUrl = ""
                };
                continue;
            }

            CurrentWorldInfoData = worldInfo;
            UpdateUi?.Invoke(worldInfo);
            Logger.Information("World info updated: {WorldName} by {AuthorName}", worldInfo.WorldName, worldInfo.AuthorName);
        }
    }
}