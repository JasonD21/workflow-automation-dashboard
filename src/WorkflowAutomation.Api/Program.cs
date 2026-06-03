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
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure;
using WorkflowAutomation.Api.Infrastructure.Email;
using WorkflowAutomation.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

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

builder.Services.AddHttpClient<IOAuthProvider, SlackOAuthProvider>();
builder.Services.AddHttpClient<IOAuthProvider, QboOAuthProvider>();
builder.Services.AddHttpClient<IOAuthProvider, GoogleOAuthProvider>();
builder.Services.AddHttpClient<IActionExecutor, SlackPostMessageExecutor>();
builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
builder.Services.AddHttpClient<IActionExecutor, GoogleCreateEventExecutor>();
builder.Services.AddHttpClient<IQboClient, QboClient>();
builder.Services.AddHttpClient<IGoogleCalendarClient, GoogleCalendarClient>();

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

builder.Services.AddSingleton<ITokenProtector, TokenProtector>();
builder.Services.AddSingleton<IOAuthStateService, OAuthStateService>();
builder.Services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// Apply migrations at startup — safe with a single instance.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
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
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapConnectionEndpoints();
app.MapCatalogEndpoints();
app.MapAutomationEndpoints();
app.MapRunEndpoints();
app.MapSlackWebhook();
app.MapQboWebhook();

app.MapHealthChecks("/health");
app.MapHangfireDashboard("/hangfire", new DashboardOptions  // localhost-only by default; we secure it for prod in the Auth chunk
{
    Authorization = [new HangfireDashboardAuth(builder.Configuration["Hangfire:DashboardKey"] ?? "")]
});

app.Run();