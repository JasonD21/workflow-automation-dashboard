using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Automations.Execution;
using WorkflowAutomation.Api.Automations.Triggers;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Connections.Webhooks;
using WorkflowAutomation.Api.Dashboard;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure;
using WorkflowAutomation.Api.Infrastructure.Email;
using WorkflowAutomation.Api.Infrastructure.Persistence;
using WorkflowAutomation.Api.Reporting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");


const string SpaCors = "spa";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddPolicy(SpaCors, p =>
    p.WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    )
);

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();
builder.Services.AddHangfire(c => c.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();
builder.Services.AddHealthChecks().AddNpgSql(connectionString);

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)   // tighten the default 5-min grace
        };
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.Configure<SlackOptions>(builder.Configuration.GetSection(SlackOptions.SectionName));
builder.Services.Configure<QboOptions>(builder.Configuration.GetSection(QboOptions.SectionName));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.Configure<ResendOptions>(builder.Configuration.GetSection(ResendOptions.SectionName));

// Configure forwarded headers options
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Render's proxy IPs are dynamic – don't use KnownNetworks/KnownProxies
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHttpClient<IOAuthProvider, SlackOAuthProvider>();
builder.Services.AddHttpClient<IOAuthProvider, QboOAuthProvider>();
builder.Services.AddHttpClient<IOAuthProvider, GoogleOAuthProvider>();
builder.Services.AddHttpClient<IActionExecutor, SlackPostMessageExecutor>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
builder.Services.AddHttpClient<IActionExecutor, GoogleCreateEventExecutor>();
builder.Services.AddHttpClient<IQboClient, QboClient>();
builder.Services.AddHttpClient<IGoogleCalendarClient, GoogleCalendarClient>();
builder.Services.AddHttpClient<ISlackClient, SlackClient>();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOAuthProviderResolver, OAuthProviderResolver>();
builder.Services.AddScoped<IConnectionsService, ConnectionsService>();
builder.Services.AddScoped<IAutomationsService, AutomationsService>();
builder.Services.AddScoped<IActionExecutorResolver, ActionExecutorResolver>();
builder.Services.AddScoped<IConnectionTokenAccessor, ConnectionTokenAccessor>();
builder.Services.AddScoped<IAutomationRunner, AutomationRunner>();
builder.Services.AddScoped<IActionExecutor, EmailSendExecutor>();
builder.Services.AddScoped<ITriggerProcessor, TriggerProcessor>();
builder.Services.AddScoped<ProcessTriggerJob>();
builder.Services.AddScoped<IConnectionRefresher, ConnectionRefresher>();
builder.Services.AddScoped<TokenRefreshSweep>();
builder.Services.AddScoped<QboEntityChangeJob>();
builder.Services.AddScoped<CalendarPollJob>();
builder.Services.AddScoped<IReportScheduleService, ReportScheduleService>();
builder.Services.AddScoped<IReportGenerator, ReportGenerator>();
builder.Services.AddScoped<GenerateReportJob>();
builder.Services.AddScoped<IReportBuilder, ReportBuilder>();
builder.Services.AddScoped<DemoSeeder>();

builder.Services.AddSingleton<ITokenProtector, TokenProtector>();
builder.Services.AddSingleton<IOAuthStateService, OAuthStateService>();
builder.Services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
builder.Services.AddSingleton<IReportScheduler, ReportScheduler>();
builder.Services.AddSingleton<IReportHtmlRenderer, ReportHtmlRenderer>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// Apply migrations at startup — safe with a single instance.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    var sp = scope.ServiceProvider;
    var dbx = sp.GetRequiredService<AppDbContext>();
    var schedule = sp.GetRequiredService<IReportScheduler>();

    foreach (var s in await dbx.ReportSchedules.Where(s => s.IsEnabled).ToListAsync())
        schedule.Sync(s);

    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<TokenRefreshSweep>("token-refresh-sweep", j => j.RunAsync(), "*/15 * * * *");
app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<CalendarPollJob>("calendar-poll", j => j.RunAsync(), "*/5 * * * *");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapDevTriggerEndpoints();
    app.MapGet("/api/dev/report-preview", async (ClaimsPrincipal u, IReportBuilder builder, CancellationToken ct) =>
    {
        var end = DateTimeOffset.UtcNow;
        var data = await builder.BuildAsync(u.GetUserId(),
            ["QuickBooks", "GoogleCalendar", "Slack"], end.AddDays(-7), end, ct);
        return Results.Ok(data);
    }).RequireAuthorization();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseCors(SpaCors);
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true
        && ctx.User.FindFirst("demo")?.Value == "true"
        && !HttpMethods.IsGet(ctx.Request.Method))
    {
        var path = ctx.Request.Path.Value ?? "";
        var allowed = path.EndsWith("/test") || path.EndsWith("/generate")
            || path.StartsWith("/api/auth");
        if (!allowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "Demo mode is read-only." });
            return;
        }
    }
    await next();
});

app.MapAuthEndpoints();
app.MapConnectionEndpoints();
app.MapCatalogEndpoints();
app.MapAutomationEndpoints();
app.MapRunEndpoints();
app.MapSlackWebhook();
app.MapQboWebhook();
app.MapReportEndpoints();
app.MapDashboardEndpoints();

app.MapHealthChecks("/health");
app.MapHangfireDashboard("/hangfire", new DashboardOptions  // localhost-only by default; we secure it for prod in the Auth chunk
{
    Authorization = [new HangfireDashboardAuth(builder.Configuration["Hangfire:DashboardKey"] ?? "")]
});

app.Run();