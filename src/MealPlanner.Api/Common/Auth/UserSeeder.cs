using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Auth;

/// <summary>
/// Creates the users listed under "Users" in configuration (user secrets locally, environment on the NUC).
/// Existing users are left alone, so changing a password in configuration has no effect after the first run.
/// </summary>
public class UserSeeder(IServiceProvider services, IConfiguration configuration, ILogger<UserSeeder> logger) : IHostedService
{
    public sealed record ConfiguredUser(string Email, string DisplayName, string Password);

    public async Task StartAsync(CancellationToken ct)
    {
        var configured = configuration.GetSection("Users").Get<List<ConfiguredUser>>() ?? [];
        configured = [.. configured.Where(u => !string.IsNullOrWhiteSpace(u.Email))];
        if (configured.Count == 0)
        {
            logger.LogWarning("No users configured under 'Users'; nobody can log in");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // v1 has exactly one household, created by SeedData.
        var householdId = await db.Households.OrderBy(h => h.Id).Select(h => h.Id).FirstAsync(ct);

        foreach (var entry in configured)
        {
            if (await userManager.FindByEmailAsync(entry.Email) is not null) continue;

            var user = new User
            {
                UserName = entry.Email,
                Email = entry.Email,
                EmailConfirmed = true,
                DisplayName = entry.DisplayName,
                HouseholdId = householdId,
            };
            var result = await userManager.CreateAsync(user, entry.Password);
            if (result.Succeeded)
                logger.LogInformation("Created user {Email}", entry.Email);
            else
                logger.LogError("Could not create user {Email}: {Errors}", entry.Email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
