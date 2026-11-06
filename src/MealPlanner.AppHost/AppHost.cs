var builder = DistributedApplication.CreateBuilder(args);

// Local development only. The developer's own SQL Server is used; in production the API runs in IIS.
var database = builder.AddConnectionString("mealplanner");

builder.AddProject<Projects.MealPlanner_Api>("api")
    .WithReference(database)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
