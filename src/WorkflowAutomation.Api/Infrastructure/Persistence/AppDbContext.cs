using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Reporting;

namespace WorkflowAutomation.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<ProviderToken> ProviderTokens => Set<ProviderToken>();
    public DbSet<Automation> Automations => Set<Automation>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<GeneratedReport> GeneratedReports => Set<GeneratedReport>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Connection>(e =>
        {
            e.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ProviderAccountId).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasOne(x => x.Token)
                .WithOne(t => t.Connection)
                .HasForeignKey<ProviderToken>(t => t.ConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Provider });
        });

        b.Entity<ProviderToken>(e =>
            e.Property(x => x.AccessTokenEncrypted).IsRequired());

        b.Entity<Automation>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.TriggerProvider).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ActionProvider).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.TriggerType).HasMaxLength(64);
            e.Property(x => x.ActionType).HasMaxLength(64);
            e.Property(x => x.FilterConfig).HasColumnType("jsonb");
            e.Property(x => x.ActionConfig).HasColumnType("jsonb");
            e.Property(x => x.TriggerConfig).HasColumnType("jsonb");
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.TriggerConnectionId);
        });

        b.Entity<AutomationRun>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.IdempotencyKey).HasMaxLength(256);
            e.Property(x => x.TriggerPayloadSummary).HasColumnType("jsonb");
            e.Property(x => x.ActionResultSummary).HasColumnType("jsonb");
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.AutomationId, x.IdempotencyKey });
        });

        b.Entity<ReportSchedule>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.TimeZone).HasMaxLength(64);
            e.Property(x => x.RecipientEmail).HasMaxLength(256);
            e.Property(x => x.IncludedSources).HasColumnType("jsonb");
            e.HasIndex(x => x.UserId);
        });

        b.Entity<GeneratedReport>(e =>
        {
            e.Property(x => x.EmailStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DataSnapshot).HasColumnType("jsonb");
            e.HasOne<ReportSchedule>()
                .WithMany()
                .HasForeignKey(x => x.ReportScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ReportScheduleId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(256);
            e.HasIndex(x => x.TokenHash);
            e.HasIndex(x => x.UserId);
        });
    }
}