using SampleApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddAuthorization();

var app = builder.Build();
app.MapControllers();
app.MapProductEndpoints();

app.MapGet("/ping", () => "pong");
app.MapPost("/webhooks/{provider}", (string provider, Stream payload) => Results.Accepted());

app.Run();
