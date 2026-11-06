var builder = DistributedApplication.CreateBuilder(args);

// Local development only. The developer's own SQL Server is used; in production the API runs in IIS.
var database = builder.AddConnectionString("mealplanner");

var api = builder.AddProject<Projects.MealPlanner_Api>("api")
    .WithReference(database)
    .WithHttpHealthCheck("/health");

// Angular dev server on :4200 with live reload; it proxies /api, /hubs, /account and /hangfire to the API on :5169
// (web/proxy.conf.json). Packages are not installed here so package-lock.json is never rewritten: run "npm ci" once.
builder.AddJavaScriptApp("web", "../../web", "start")
    .WithNpm(install: false)
    .WithHttpEndpoint(port: 4200, isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(api);

builder.Build().Run();
