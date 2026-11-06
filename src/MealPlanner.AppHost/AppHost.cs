var builder = DistributedApplication.CreateBuilder(args);

if (builder.ExecutionContext.IsPublishMode)
    AddNucDeployment(builder);
else
    AddLocalDevelopment(builder);

builder.Build().Run();

// Local: the developer's own SQL Server (no Docker on the dev machine), API run as a project.
static void AddLocalDevelopment(IDistributedApplicationBuilder builder)
{
    var database = builder.AddConnectionString("mealplanner");

    builder.AddProject<Projects.MealPlanner_Api>("api")
        .WithReference(database)
        .WithHttpHealthCheck("/health");
}

// NUC: `aspire publish` turns this into deploy/docker-compose.yaml plus a .env with the parameters below.
static void AddNucDeployment(IDistributedApplicationBuilder builder)
{
    builder.AddDockerComposeEnvironment("nuc")
        .WithDashboard(false);

    var sqlPassword = builder.AddParameter("sql-password", secret: true);
    var database = builder.AddSqlServer("sql", sqlPassword)
        .WithEnvironment("MSSQL_PID", "Express")
        .WithDataVolume("mealplanner-sql")
        .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped")
        .AddDatabase("mealplanner", "MealPlanner");

    // Built on the NUC from the repo root, so the Dockerfile sees Directory.*.props and ServiceDefaults.
    var api = builder.AddDockerfile("api", "../..", "src/MealPlanner.Api/Dockerfile")
        .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
        .WithExternalHttpEndpoints()
        // Only the connection string; WithReference would also add host/user/password variables we don't use.
        .WithEnvironment("ConnectionStrings__mealplanner", database.Resource.ConnectionStringExpression)
        .WaitFor(database)
        .WithEnvironment("TZ", "Europe/Copenhagen")
        .WithEnvironment("Users__0__DisplayName", "Marc")
        .WithEnvironment("Users__0__Email", builder.AddParameter("marc-email"))
        .WithEnvironment("Users__0__Password", builder.AddParameter("marc-password", secret: true))
        .WithEnvironment("Users__1__DisplayName", "Pia")
        .WithEnvironment("Users__1__Email", builder.AddParameter("pia-email"))
        .WithEnvironment("Users__1__Password", builder.AddParameter("pia-password", secret: true))
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";
            // Paths are relative to deploy/docker-compose.yaml: `docker compose up -d --build` builds from the repo.
            service.Image = "mealplanner-api:latest";
            service.Build = new() { Context = "..", Dockerfile = "src/MealPlanner.Api/Dockerfile" };
        });

    // The only way in from outside. Public hostname and path rules are set up in the Cloudflare dashboard.
    builder.AddContainer("cloudflared", "cloudflare/cloudflared", "2026.10.7")
        .WithArgs("tunnel", "--no-autoupdate", "run")
        .WithEnvironment("TUNNEL_TOKEN", builder.AddParameter("cloudflare-tunnel-token", secret: true))
        .WaitFor(api)
        .PublishAsDockerComposeService((_, service) => service.Restart = "unless-stopped");
}
