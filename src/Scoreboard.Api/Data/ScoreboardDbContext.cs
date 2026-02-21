using Microsoft.EntityFrameworkCore;
using Scoreboard.Api.Data.Entities;

namespace Scoreboard.Api.Data;

public class ScoreboardDbContext : DbContext
{
	public ScoreboardDbContext(DbContextOptions<ScoreboardDbContext> options) : base(options) { }

	public DbSet<Sport> Sports => Set<Sport>();
	public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
	public DbSet<Discount> Discounts => Set<Discount>();
	public DbSet<Streamer> Streamers => Set<Streamer>();
	public DbSet<Team> Teams => Set<Team>();
	public DbSet<Game> Games => Set<Game>();
	public DbSet<GameTeamStats> GameTeamStats => Set<GameTeamStats>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Sport
		modelBuilder.Entity<Sport>(e =>
		{
			e.HasKey(s => s.SportId);
			e.HasIndex(s => s.SportCode).IsUnique();
		});

		// SubscriptionPlan
		modelBuilder.Entity<SubscriptionPlan>(e =>
		{
			e.HasKey(s => s.SubscriptionPlanId);
			e.HasIndex(s => s.PlanCode).IsUnique();
			e.Property(s => s.PriceAmount).HasColumnType("decimal(10,2)");
			e.Property(s => s.DiscountPercent).HasColumnType("decimal(5,2)");
		});

		// Discount
		modelBuilder.Entity<Discount>(e =>
		{
			e.HasKey(d => d.DiscountId);
			e.HasIndex(d => d.CouponCode).IsUnique();
			e.Property(d => d.DiscountPercent).HasColumnType("decimal(5,2)");
			e.Property(d => d.DiscountAmount).HasColumnType("decimal(10,2)");
		});

		// Streamer
		modelBuilder.Entity<Streamer>(e =>
		{
			e.HasKey(s => s.StreamerId);
			e.HasIndex(s => s.StreamKey).IsUnique();
			e.HasIndex(s => s.StreamToken).IsUnique();
			e.HasIndex(s => s.EmailAddress).IsUnique();
			e.HasOne(s => s.SubscriptionPlan)
				.WithMany()
				.HasForeignKey(s => s.SubscriptionPlanId)
				.OnDelete(DeleteBehavior.SetNull);
			e.HasOne(s => s.DiscountApplied)
				.WithMany()
				.HasForeignKey(s => s.DiscountId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		// Team
		modelBuilder.Entity<Team>(e =>
		{
			e.HasKey(t => t.TeamId);
			e.HasIndex(t => new { t.StreamerId, t.TeamCode }).IsUnique();
			e.HasOne(t => t.Streamer)
				.WithMany(s => s.Teams)
				.HasForeignKey(t => t.StreamerId)
				.OnDelete(DeleteBehavior.Cascade);
			e.HasOne(t => t.Sport)
				.WithMany()
				.HasForeignKey(t => t.SportId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		// Game
		modelBuilder.Entity<Game>(e =>
		{
			e.HasKey(g => g.GameId);
			e.HasOne(g => g.Streamer)
				.WithMany(s => s.Games)
				.HasForeignKey(g => g.StreamerId)
				.OnDelete(DeleteBehavior.Cascade);
			e.HasOne(g => g.Sport)
				.WithMany()
				.HasForeignKey(g => g.SportId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne(g => g.HomeTeam)
				.WithMany()
				.HasForeignKey(g => g.HomeTeamId)
				.OnDelete(DeleteBehavior.Restrict);
			e.HasOne(g => g.AwayTeam)
				.WithMany()
				.HasForeignKey(g => g.AwayTeamId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		// GameTeamStats
		modelBuilder.Entity<GameTeamStats>(e =>
		{
			e.HasKey(g => g.GameTeamStatsId);
			e.HasIndex(g => new { g.GameId, g.TeamId }).IsUnique();
			e.HasOne(g => g.Game)
				.WithMany(g => g.TeamStats)
				.HasForeignKey(g => g.GameId)
				.OnDelete(DeleteBehavior.Cascade);
			e.HasOne(g => g.Team)
				.WithMany()
				.HasForeignKey(g => g.TeamId)
				.OnDelete(DeleteBehavior.Restrict);
		});
	}
}
