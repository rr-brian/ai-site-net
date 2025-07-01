var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register application services
builder.Services.AddScoped<Backend.Services.DocumentProcessingService>();
builder.Services.AddScoped<Backend.Services.DocumentChunkingService>();
builder.Services.AddScoped<Backend.Services.OpenAIService>();
builder.Services.AddScoped<Backend.Services.AzureFunctionService>();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure default files (must be before UseStaticFiles)
app.UseDefaultFiles();

// Use static files from the wwwroot folder
app.UseStaticFiles();

// Add cache control headers for HTML files to prevent browser caching
app.Use(async (context, next) => {
    if (context.Request.Path.Value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.Value == "/") {
        context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Add("Pragma", "no-cache");
        context.Response.Headers.Add("Expires", "0");
    }
    await next();
});

// Use CORS
app.UseCors("AllowAll");

// Use session
app.UseSession();

// Map controllers
app.MapControllers();

app.Run();
