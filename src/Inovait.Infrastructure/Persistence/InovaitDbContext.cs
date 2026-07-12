using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.People;
using Inovait.Core.Domain.Staff;
using Inovait.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence;

public sealed class InovaitDbContext(DbContextOptions<InovaitDbContext> options)
    : DbContext(options)
{
    public DbSet<School> Schools => Set<School>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<AcademicConfiguration> AcademicConfigurations => Set<AcademicConfiguration>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<TeacherContract> TeacherContracts => Set<TeacherContract>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InovaitDbContext).Assembly);
        ProductionCatalogSeed.Apply(modelBuilder);
    }
}
