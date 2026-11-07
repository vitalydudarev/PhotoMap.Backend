using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
    {
        public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Topic).IsRequired();
            builder.Property(a => a.Key).IsRequired();
            // text rather than jsonb: the payload is handed to Kafka as it is, never queried, and jsonb would not
            // keep it byte for byte
            builder.Property(a => a.Payload).IsRequired();
            builder.Property(a => a.CreatedOn).IsRequired();
            builder.Property(a => a.Attempts).IsRequired();
            builder.ToTable("outbox_messages");

            // the relay looks for the messages waiting, in order; the published ones are kept for a while, and an
            // index of only the waiting ones stays small however many of them there are
            builder.HasIndex(a => a.Id)
                .HasDatabaseName("ix_outbox_messages_pending")
                .HasFilter("published_on IS NULL");

            // the published ones are deleted by when they were published
            builder.HasIndex(a => a.PublishedOn);
        }
    }
}
