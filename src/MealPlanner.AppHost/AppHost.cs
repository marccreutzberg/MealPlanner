var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MealPlanner_Api>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
