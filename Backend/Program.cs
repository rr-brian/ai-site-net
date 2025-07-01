var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClient factory
builder.Services.AddHttpClient();

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

// Use CORS
app.UseCors("AllowAll");

// Map controllers
app.MapControllers();

app.Run();
