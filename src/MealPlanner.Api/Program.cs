using MealPlanner.Api.Common.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("mealplanner");

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.MapDefaultEndpoints();

app.Run();
