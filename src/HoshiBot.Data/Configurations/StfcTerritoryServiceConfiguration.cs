using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcTerritoryServiceConfiguration : IEntityTypeConfiguration<StfcTerritoryService>
{
    public void Configure(EntityTypeBuilder<StfcTerritoryService> builder)
    {
        builder.HasKey(s => s.Id);
        // Id is the real Scopely service spec id (svid), assigned by the sync — never generated.
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
    }
}
