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

    public DbSet<WatchlistGroup> WatchlistGroups => Set<WatchlistGroup>();

    public DbSet<CashAccount> CashAccounts => Set<CashAccount>();

    public DbSet<Holding> Holdings => Set<Holding>();

    public DbSet<SimulatedOrder> Orders => Set<SimulatedOrder>();

    public DbSet<OrderExecution> OrderExecutions => Set<OrderExecution>();

    public DbSet<HoldingSettlementLot> HoldingSettlementLots => Set<HoldingSettlementLot>();

    public DbSet<SettlementRun> SettlementRuns => Set<SettlementRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(ConfigureUserAccount);
        modelBuilder.Entity<WatchlistGroup>(ConfigureWatchlistGroup);
        modelBuilder.Entity<WatchlistItem>(ConfigureWatchlistItem);
        modelBuilder.Entity<CashAccount>(ConfigureCashAccount);
        modelBuilder.Entity<Holding>(ConfigureHolding);
        modelBuilder.Entity<SimulatedOrder>(ConfigureSimulatedOrder);
        modelBuilder.Entity<OrderExecution>(ConfigureOrderExecution);
        modelBuilder.Entity<HoldingSettlementLot>(ConfigureHoldingSettlementLot);
        modelBuilder.Entity<SettlementRun>(ConfigureSettlementRun);
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

        entity.HasMany(user => user.WatchlistGroups)
            .WithOne(group => group.User)
            .HasForeignKey(group => group.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(user => user.WatchlistGroups)
            .HasField("_watchlistGroups")
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

    private static void ConfigureWatchlistGroup(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<WatchlistGroup> entity)
    {
        entity.ToTable("WatchlistGroups");
        entity.HasKey(group => group.Id);

        entity.Property(group => group.Name)
            .HasMaxLength(64)
            .IsRequired();

        entity.HasIndex(group => new { group.UserId, group.Name })
            .IsUnique();

        entity.HasMany(group => group.Items)
            .WithOne(item => item.Group)
            .HasForeignKey(item => item.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.Navigation(group => group.Items)
            .HasField("_items")
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

        entity.HasIndex(item => new { item.GroupId, item.BoardId, item.Symbol })
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
        entity.Property(order => order.OrderType)
            .HasDefaultValue(OrderType.LO);
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

    private static void ConfigureHoldingSettlementLot(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<HoldingSettlementLot> entity)
    {
        entity.ToTable("HoldingSettlementLots");
        entity.HasKey(lot => lot.Id);

        entity.Property(lot => lot.Symbol)
            .HasMaxLength(16)
            .IsRequired();
        entity.Property(lot => lot.BoardId)
            .HasMaxLength(8)
            .IsRequired();

        entity.HasIndex(lot => new { lot.UserId, lot.BoardId, lot.Symbol, lot.Status });
        entity.HasIndex(lot => new { lot.UserId, lot.AvailableFromDate, lot.Status });
        entity.HasIndex(lot => lot.SourceOrderId);
        entity.HasIndex(lot => lot.SourceExecutionId);

        entity.HasOne(lot => lot.User)
            .WithMany()
            .HasForeignKey(lot => lot.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(lot => lot.SourceOrder)
            .WithMany()
            .HasForeignKey(lot => lot.SourceOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSettlementRun(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SettlementRun> entity)
    {
        entity.ToTable("SettlementRuns");
        entity.HasKey(run => run.Id);

        entity.HasIndex(run => run.StartedAt);
        entity.HasIndex(run => run.TriggeredByUserId);
    }
}
