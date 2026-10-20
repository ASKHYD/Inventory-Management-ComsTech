using System;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using SOFT2027.Data.Repositories;
using SOFT2027.Data.Services;

namespace SOFT2027.Tests;

public sealed class AdminUserRepositoryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"soft2027-admin-{Guid.NewGuid():N}.db");
    private string SchemaPath => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "docs", "DATABASE_SCHEMA.sql"));
    private AdminUserRepository Repository => new(new SqliteConnectionFactory(_path));

    public async Task InitializeAsync() => await new DatabaseInitializer(new SqliteConnectionFactory(_path), SchemaPath).InitializeAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FirstRunSetup_ShouldHashPasswordAuditCreationAndRejectInvalidCredentials()
    {
        var repository = Repository;
        var admin = await repository.CreateFirstAdminAsync("owner", "Business Owner", "correct-horse-battery");
        admin.Status.Should().Be("Active");

        using var connection = new SqliteConnection($"Data Source={_path};");
        await connection.OpenAsync();
        var stored = await connection.ExecuteScalarAsync<string>("SELECT PasswordHash FROM AdminUsers WHERE AdminId=@Id", new { Id = admin.AdminId });
        stored.Should().StartWith("PBKDF2-SHA256$").And.NotContain("correct-horse-battery");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE AdminId=@Id AND ModuleName='AdminUsers' AND Action='CREATE'", new { Id = admin.AdminId })).Should().Be(1);

        (await repository.AuthenticateAsync("owner", "wrong-password")).Should().BeNull();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE ModuleName='Security' AND Action='LOGIN'")).Should().Be(0);
        (await repository.AuthenticateAsync("owner", "correct-horse-battery")).Should().NotBeNull();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE ModuleName='Security' AND Action='LOGIN'")).Should().Be(1);
        Func<Task> secondSetup = () => repository.CreateFirstAdminAsync("second", "Second Admin", "another-safe-password");
        await secondSetup.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already been completed*");
    }

    [Fact]
    public async Task AdministratorDeactivationAndPasswordReset_ShouldBeAuditedAndKeepOneActiveAdmin()
    {
        var repository = Repository;
        var first = await repository.CreateFirstAdminAsync("owner", "Business Owner", "correct-horse-battery");
        var secondId = await repository.CreateAdminAsync("manager", "Manager", "another-safe-password", first.AdminId);

        await repository.SetStatusAsync(secondId, false, first.AdminId);
        Func<Task> deactivateLast = () => repository.SetStatusAsync(first.AdminId, false, first.AdminId);
        await deactivateLast.Should().ThrowAsync<InvalidOperationException>().WithMessage("*At least one active administrator*");
        await repository.ResetPasswordAsync(secondId, "replacement-safe-password", first.AdminId);
        (await repository.AuthenticateAsync("manager", "another-safe-password")).Should().BeNull();
        await repository.SetStatusAsync(secondId, true, first.AdminId);
        (await repository.AuthenticateAsync("manager", "replacement-safe-password")).Should().NotBeNull();

        using var connection = new SqliteConnection($"Data Source={_path};");
        await connection.OpenAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE ModuleName='AdminUsers' AND Action='CREATE'")).Should().Be(2);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AuditLogs WHERE ModuleName='AdminUsers' AND Action='UPDATE'")).Should().Be(3);
    }
}
