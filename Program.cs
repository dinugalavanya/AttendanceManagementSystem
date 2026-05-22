using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Swagger for API testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "Attendance Management System API", 
        Version = "v1",
        Description = "API for managing attendance, users, and sections"
    });
    
    // Add XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Authentication
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.Cookie.Name = "UserLoginCookie";
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Register custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<DatabaseMigrationService>();
builder.Services.AddScoped<AttendanceCalculationService>();

var app = builder.Build();

// Startup database diagnostics and connectivity check
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        logger.LogError("DefaultConnection is missing or empty.");
        throw new InvalidOperationException("DefaultConnection is missing or empty.");
    }
    else
    {
        var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
        var authMode = sqlBuilder.IntegratedSecurity ? "Windows Authentication" : "SQL Authentication";

        logger.LogInformation(
            "Database target configured: Server={Server}; Database={Database}; AuthMode={AuthMode}",
            sqlBuilder.DataSource,
            sqlBuilder.InitialCatalog,
            authMode);

        if (sqlBuilder.IntegratedSecurity)
        {
            logger.LogInformation("Windows identity for DB connection: {WindowsUser}", $"{Environment.UserDomainName}\\{Environment.UserName}");
        }

        try
        {
            logger.LogInformation("Starting database migration and initialization.");
            var dbMigration = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
            await dbMigration.InitializeAsync();

            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await DatabaseInitializer.EnsureCoreDataAsync(dbContext, logger);

            logger.LogInformation("Database initialization completed successfully for Server={Server}; Database={Database}", sqlBuilder.DataSource, sqlBuilder.InitialCatalog);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed for Server={Server}; Database={Database}: {Message}", sqlBuilder.DataSource, sqlBuilder.InitialCatalog, ex.Message);
            logger.LogWarning("Continuing application startup without a completed database initialization. Login and other database-backed actions may still fail until the database issue is resolved.");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Attendance Management API V1");
        c.RoutePrefix = "swagger"; // Set Swagger UI at /swagger
    });
}

// Use HTTPS redirection only in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

// Use authentication and session
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");


app.Run();
