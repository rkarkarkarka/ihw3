using Microsoft.EntityFrameworkCore;
using OrdersService.Models;

namespace OrdersService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            // Индексы для оптимизации запросов
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => e.Status);
        });

        // Конфигурация OutboxMessage
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.OccurredOn).IsRequired();
            entity.HasIndex(e => new { e.ProcessedOn, e.OccurredOn });
            entity.HasIndex(e => new { e.Type, e.ProcessedOn });
        });

        // Конфигурация InboxMessage
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.OccurredOn).IsRequired();
            entity.HasIndex(e => new { e.ProcessedOn, e.OccurredOn });
            entity.HasIndex(e => new { e.Type, e.ProcessedOn });
        });
    }
}
