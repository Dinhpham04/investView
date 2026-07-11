using InvestView.Domain.Trading;
using Microsoft.EntityFrameworkCore;

namespace InvestView.Infrastructure.Data;

public sealed class InvestViewDbContext : DbContext
{
    public InvestViewDbContext(DbContextOptions<InvestViewDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<CashAccount> CashAccounts => Set<CashAccount>();

    public DbSet<Holding> Holdings => Set<Holding>();

    public DbSet<SimulatedOrder> Orders => Set<SimulatedOrder>();

    public DbSet<OrderExecution> OrderExecutions => Set<OrderExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(ConfigureUserAccount);
        modelBuilder.Entity<WatchlistItem>(ConfigureWatchlistItem);
        modelBuilder.Entity<CashAccount>(ConfigureCashAccount);
        modelBuilder.Entity<Holding>(ConfigureHolding);
        modelBuilder.Entity<SimulatedOrder>(ConfigureSimulatedOrder);
        modelBuilder.Entity<OrderExecution>(ConfigureOrderExecution);
    }

    private static void ConfigureUserAccount(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<UserAccount> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(user => user.Id);

        entity.Property(user => user.Email)
            .HasMaxLength(254)
            .IsRequired();
        entity.HasIndex(user => user.Email)
            .IsUnique();

        entity.Property(user => user.DisplayName)
            .HasMaxLength(128)
            .IsRequired();

        entity.Property(user => user.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        entity.HasMany(user => user.WatchlistItems)
            .WithOne(item => item.User)
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(user => user.WatchlistItems)
            .HasField("_watchlistItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasMany(user => user.CashAccounts)
            .WithOne(account => account.User)
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(user => user.CashAccounts)
            .HasField("_cashAccounts")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasMany(user => user.Holdings)
            .WithOne(holding => holding.User)
            .HasForeignKey(holding => holding.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(user => user.Holdings)
            .HasField("_holdings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        entity.HasMany(user => user.Orders)
            .WithOne(order => order.User)
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(user => user.Orders)
            .HasField("_orders")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureWatchlistItem(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<WatchlistItem> entity)
    {
        entity.ToTable("WatchlistItems");
        entity.HasKey(item => item.Id);

        entity.Property(item => item.Symbol)
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(item => item.BoardId)
            .HasMaxLength(8)
            .IsRequired();

        entity.HasIndex(item => new { item.UserId, item.BoardId, item.Symbol })
            .IsUnique();
    }

    private static void ConfigureCashAccount(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<CashAccount> entity)
    {
        entity.ToTable("CashAccounts");
        entity.HasKey(account => account.Id);

        entity.Property(account => account.Currency)
            .HasMaxLength(8)
            .IsRequired();
        entity.Property(account => account.Balance)
            .HasPrecision(18, 2);
        entity.Property(account => account.AvailableBalance)
            .HasPrecision(18, 2);

        entity.HasIndex(account => new { account.UserId, account.Currency })
            .IsUnique();
    }

    private static void ConfigureHolding(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Holding> entity)
    {
        entity.ToTable("Holdings");
        entity.HasKey(holding => holding.Id);

        entity.Property(holding => holding.Symbol)
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(holding => holding.BoardId)
            .HasMaxLength(8)
            .IsRequired();
        entity.Property(holding => holding.AverageCost)
            .HasPrecision(18, 4);

        entity.HasIndex(holding => new { holding.UserId, holding.BoardId, holding.Symbol })
            .IsUnique();
    }

    private static void ConfigureSimulatedOrder(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SimulatedOrder> entity)
    {
        entity.ToTable("Orders");
        entity.HasKey(order => order.Id);

        entity.Property(order => order.Symbol)
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(order => order.BoardId)
            .HasMaxLength(8)
            .IsRequired();
        entity.Property(order => order.LimitPrice)
            .HasPrecision(18, 4);
        entity.Property(order => order.AverageFillPrice)
            .HasPrecision(18, 4);

        entity.HasIndex(order => new { order.UserId, order.CreatedAt });
        entity.HasIndex(order => new { order.UserId, order.Status });

        entity.HasMany(order => order.Executions)
            .WithOne(execution => execution.Order)
            .HasForeignKey(execution => execution.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(order => order.Executions)
            .HasField("_executions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureOrderExecution(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OrderExecution> entity)
    {
        entity.ToTable("OrderExecutions");
        entity.HasKey(execution => execution.Id);

        entity.Property(execution => execution.Price)
            .HasPrecision(18, 4);
        entity.Property(execution => execution.GrossAmount)
            .HasPrecision(18, 2);

        entity.HasIndex(execution => new { execution.OrderId, execution.ExecutedAt });
    }
}
