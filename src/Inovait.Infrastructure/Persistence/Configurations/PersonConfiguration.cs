using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Person", "people", table =>
        {
            table.HasCheckConstraint("CK_Person_DocumentNumber_NotBlank", "LEN(TRIM([DocumentNumber])) > 0");
            table.HasCheckConstraint("CK_Person_FirstNames_NotBlank", "LEN(TRIM([FirstNames])) > 0");
            table.HasCheckConstraint("CK_Person_LastNames_NotBlank", "LEN(TRIM([LastNames])) > 0");
            table.HasCheckConstraint("CK_Person_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
        });
        builder.HasKey(entity => entity.Id).HasName("PK_Person");
        builder.Property(entity => entity.DocumentTypeId).HasColumnType("smallint");
        builder.Property(entity => entity.DocumentNumber).HasColumnType("nvarchar(32)").HasMaxLength(32)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.FirstNames).HasColumnType("nvarchar(120)").HasMaxLength(120)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.LastNames).HasColumnType("nvarchar(120)").HasMaxLength(120)
            .UseCollation(CatalogConfigurationConventions.Collation).IsRequired();
        builder.Property(entity => entity.BirthDate).HasColumnType("date");
        builder.HasIndex(entity => new { entity.DocumentTypeId, entity.DocumentNumber }).IsUnique()
            .HasDatabaseName("UQ_Person_DocumentTypeId_DocumentNumber");
        builder.HasIndex(entity => new { entity.LastNames, entity.FirstNames, entity.Id })
            .IncludeProperties(entity => new { entity.DocumentTypeId, entity.DocumentNumber, entity.BirthDate })
            .HasDatabaseName("IX_Person_LastNames_FirstNames_Id");
        builder.HasOne<DocumentType>().WithMany().HasForeignKey(entity => entity.DocumentTypeId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Person_DocumentType");
        CatalogConfigurationConventions.ConfigureAudit(builder);
    }
}
