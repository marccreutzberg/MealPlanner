var builder = DistributedApplication.CreateBuilder(args);

// Locally the developer's own SQL Server is used (no Docker on the dev machine).
var database = builder.AddConnectionString("mealplanner");

builder.AddProject<Projects.MealPlanner_Api>("api")
    .WithReference(database)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
