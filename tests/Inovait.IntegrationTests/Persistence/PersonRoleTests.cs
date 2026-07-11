using Inovait.Core.Domain.People;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Inovait.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Priority", "P0")]
public sealed class PersonRoleTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private InovaitDbContext _context = null!;
    [Fact]
    [Trait("Evidence", "IT-PERSON-COLLATION")]
    public async Task PersonDocumentIdentity_IsCaseInsensitiveAccentSensitiveAndNfc()
    {
        var person = new Person(1, " ab\t123 ", "Jose\u0301", " Pérez ", new(2010, 4, 2));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(("ab 123", "José", "Pérez"), (person.DocumentNumber, person.FirstNames, person.LastNames));
        var duplicate = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlRawAsync(
            "INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate]) VALUES (1,N'AB 123',N'Other',N'Person','2011-01-01')",
            TestContext.Current.CancellationToken));
        Assert.True(duplicate.Number is 2601 or 2627);
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate]) VALUES (1,N'áb 123',N'Other',N'Person','2011-01-01')",
            TestContext.Current.CancellationToken);
        Assert.Equal(2, await _context.People.CountAsync(TestContext.Current.CancellationToken));
        var entity = Model.FindEntityType(typeof(Person))!;
        foreach (var property in new[] { nameof(Person.DocumentNumber), nameof(Person.FirstNames), nameof(Person.LastNames) })
            Assert.Equal("Latin1_General_100_CI_AS", entity.FindProperty(property)!.GetCollation());
        var unique = Assert.Single(entity.GetIndexes(), index => index.IsUnique);
        Assert.Equal("UQ_Person_DocumentTypeId_DocumentNumber", unique.GetDatabaseName());
        Assert.Equal([nameof(Person.DocumentTypeId), nameof(Person.DocumentNumber)], unique.Properties.Select(property => property.Name));
        var names = Assert.Single(entity.GetIndexes(), index => !index.IsUnique);
        Assert.Equal("IX_Person_LastNames_FirstNames_Id", names.GetDatabaseName());
        Assert.Equal([nameof(Person.LastNames), nameof(Person.FirstNames), nameof(Person.Id)],
            names.Properties.Select(property => property.Name));
        Assert.Equal([nameof(Person.DocumentTypeId), nameof(Person.DocumentNumber), nameof(Person.BirthDate)],
            names.GetIncludeProperties());
    }
    [Fact]
    [Trait("Evidence", "IT-PERSON-DUAL-ROLE")]
    public async Task IndependentPkForeignKeys_AllowBothRolesWithoutDuplicatingIdentity()
    {
        var person = new Person(1, "ROLE-01", "Dual", "Role", new(1990, 1, 1));
        _context.Add(person);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var student = new Student(person.Id);
        var teacher = new Teacher(person.Id);
        _context.AddRange(student, teacher);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal((1, 1, 1), (await _context.People.CountAsync(cancellationToken),
            await _context.Students.CountAsync(cancellationToken), await _context.Teachers.CountAsync(cancellationToken)));
        Assert.NotEmpty(person.RowVersion);
        Assert.NotEmpty(teacher.RowVersion);
        AssertAuditPolicy<Person>(true);
        AssertAuditPolicy<Teacher>(true);
        AssertAuditPolicy<Student>(false);
        var mappings = new[] { (Type: typeof(Student), Table: "Student", Key: "PK_Student", ForeignKey: "FK_Student_Person"),
            (Type: typeof(Teacher), Table: "Teacher", Key: "PK_Teacher", ForeignKey: "FK_Teacher_Person") };
        foreach (var expected in mappings)
        {
            var role = Model.FindEntityType(expected.Type)!;
            Assert.Equal(("people", expected.Table), (role.GetSchema(), role.GetTableName()));
            var key = role.FindPrimaryKey()!;
            Assert.Equal((expected.Key, "PersonId"), (key.GetName(), Assert.Single(key.Properties).Name));
            var foreignKey = Assert.Single(role.GetForeignKeys());
            Assert.Equal((expected.ForeignKey, DeleteBehavior.NoAction, "people", "Person"),
                (foreignKey.GetConstraintName(), foreignKey.DeleteBehavior,
                    foreignKey.PrincipalEntityType.GetSchema(), foreignKey.PrincipalEntityType.GetTableName()));
            Assert.Equal(("PersonId", "Id"), (Assert.Single(foreignKey.Properties).Name, Assert.Single(foreignKey.PrincipalKey.Properties).Name));
        }
    }
    [Fact]
    [Trait("Evidence", "IT-TEXT-CHECKS")]
    public async Task PersonTextChecks_DefendOrdinarySpacesWhileApplicationDefendsUnicodeWhitespace()
    {
        (string Number, string FirstNames, string LastNames)[] invalid =
            [(string.Empty, "Valid", "Person"), ("CHECK-01", "   ", "Person"), ("CHECK-02", "Valid", string.Empty)];
        foreach (var value in invalid)
        {
            var failure = await Assert.ThrowsAsync<SqlException>(() => _context.Database.ExecuteSqlAsync(
                $"INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate]) VALUES (1,{value.Number},{value.FirstNames},{value.LastNames},'2010-01-01')",
                TestContext.Current.CancellationToken));
            Assert.Equal(547, failure.Number);
        }
        Assert.Equal(["CK_Person_DocumentNumber_NotBlank", "CK_Person_FirstNames_NotBlank", "CK_Person_LastNames_NotBlank"],
            Model.FindEntityType(typeof(Person))!.GetCheckConstraints().Select(check => check.Name).OfType<string>().Where(name => name.EndsWith("_NotBlank", StringComparison.Ordinal)).Order());
        await _context.Database.ExecuteSqlRawAsync(
            "INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate]) VALUES (1,N'TAB-01',CHAR(9)+CHAR(10),N'Person','2010-01-01')",
            TestContext.Current.CancellationToken);
        _context.Add(new Person(1, "TAB-02", "\t\n", "Person", new(2010, 1, 1)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
    public async ValueTask InitializeAsync()
    {
        var connection = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitS04_{Guid.NewGuid():N}",
        }.ConnectionString;
        _provider = new ServiceCollection().AddInovaitInfrastructure(connection).BuildServiceProvider(true);
        _scope = _provider.CreateAsyncScope();
        _context = _scope.ServiceProvider.GetRequiredService<InovaitDbContext>();
        await _context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }
    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
    private IModel Model => _context.GetService<IDesignTimeModel>().Model;
    private void AssertAuditPolicy<TEntity>(bool expected) where TEntity : class
    {
        var entity = Model.FindEntityType(typeof(TEntity))!;
        Assert.Equal(expected, entity.FindProperty("CreatedAtUtc") is not null);
        Assert.Equal(expected, entity.FindProperty("UpdatedAtUtc") is not null);
        Assert.Equal(expected, entity.FindProperty("RowVersion")?.IsConcurrencyToken ?? false);
    }
}
