using CashFlow.Transactions.Domain.Aggregates;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Transactions.Infrastructure.Persistence;

public sealed class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyKeyConfiguration());
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(t => t.Type).HasColumnName("type")
            .HasConversion(v => v.ToString(), v => Enum.Parse<TransactionType>(v));
        builder.Property(t => t.Status).HasColumnName("status")
            .HasConversion(v => v.ToString(), v => Enum.Parse<TransactionStatus>(v));
        builder.Property(t => t.OccurredOn).HasColumnName("occurred_on");
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(140);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.ReversalOf).HasColumnName("reversal_of");

        builder.OwnsOne(t => t.Amount, amtNav =>
        {
            amtNav.Property(a => a.Value).HasColumnName("amount")
                .HasColumnType("numeric(18,4)").IsRequired();
            amtNav.Property(a => a.Currency).HasColumnName("currency")
                .HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(t => t.MerchantId).HasDatabaseName("ix_transactions_merchant_id");
        builder.HasIndex(t => new { t.MerchantId, t.OccurredOn })
            .HasDatabaseName("ix_transactions_merchant_occurred");
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(o => o.AggregateId).HasColumnName("aggregate_id");
        builder.Property(o => o.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(o => o.EventVersion).HasColumnName("event_version");
        builder.Property(o => o.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(o => o.OccurredAt).HasColumnName("occurred_at");
        builder.Property(o => o.PublishedAt).HasColumnName("published_at");
        builder.Property(o => o.RetryCount).HasColumnName("retry_count");

        builder.HasIndex(o => o.OccurredAt)
            .HasFilter("published_at IS NULL")
            .HasDatabaseName("ix_outbox_unpublished");
    }
}

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.MerchantId).HasColumnName("merchant_id");
        builder.Property(k => k.Key).HasColumnName("key").HasMaxLength(255).IsRequired();
        builder.Property(k => k.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(k => new { k.MerchantId, k.Key })
            .IsUnique()
            .HasDatabaseName("ix_idempotency_merchant_key");
    }
}
