using Inovait.Core.Domain.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inovait.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Student", "people");
        builder.HasKey(entity => entity.PersonId).HasName("PK_Student");
        builder.Property(entity => entity.PersonId).HasColumnType("int").ValueGeneratedNever();
        builder.HasOne<Person>().WithOne().HasForeignKey<Student>(entity => entity.PersonId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Student_Person");
    }
}
