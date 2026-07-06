using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using AttendanceManagementSystem.Data;
using AttendanceManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Attendance Management System API",
        Version = "v1",
        Description = "API for managing attendance, users, and sections"
    });
});

// EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================
// AUTHENTICATION (FIXED)
// =======================
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "CookieAuth";
    options.DefaultChallengeScheme = "AzureAd";
})
.AddCookie("CookieAuth", options =>
{
    options.Cookie.Name = "UserLoginCookie";
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
})
.AddOpenIdConnect("AzureAd", options =>
{
    var azureAd = builder.Configuration.GetSection("AzureAd");

    var tenantId = azureAd["TenantId"];
    var instance = azureAd["Instance"] ?? "https://login.microsoftonline.com/";
    var clientId = azureAd["ClientId"];
    var clientSecret = azureAd["ClientSecret"];
    var callbackPath = azureAd["CallbackPath"] ?? "/signin-oidc";

    if (string.IsNullOrWhiteSpace(tenantId))
    {
        throw new InvalidOperationException("AzureAd:TenantId is required for single-tenant authentication.");
    }

    if (string.IsNullOrWhiteSpace(clientId))
    {
        throw new InvalidOperationException("AzureAd:ClientId is required.");
    }

    if (string.IsNullOrWhiteSpace(clientSecret))
    {
        throw new InvalidOperationException("AzureAd:ClientSecret is required for Authorization Code Flow.");
    }

    // Single-tenant authority - forces organizational accounts only
    options.Authority = $"{instance.TrimEnd('/')}/{tenantId.TrimEnd('/')}/v2.0";

    options.ClientId = clientId;
    options.ClientSecret = clientSecret;

    options.CallbackPath = callbackPath.StartsWith('/') ? callbackPath : $"/{callbackPath}";
    options.SignInScheme = "CookieAuth";

    // Authorization Code Flow (required by Azure AD)
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    // Scopes for organizational accounts
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    // Let Microsoft.Identity handle all token validation automatically based on Authority
    // No manual TokenValidationParameters overrides needed

    // Events for error handling and logging
    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProvider = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("OIDC redirect to: {Authority} for ClientId: {ClientId}", context.Options.Authority, clientId);
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Failure, "OIDC remote failure: {Message}", context.Failure?.Message);
            context.HandleResponse();
            context.Response.Redirect("/Account/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Authentication failed"));
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "OIDC authentication failed: {Message}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var issuer = context.Principal?.FindFirst("iss")?.Value;
            var subject = context.Principal?.FindFirst("sub")?.Value;
            logger.LogInformation("OIDC token validated. Issuer: {Issuer}, Subject: {Subject}", issuer, subject);
            return Task.CompletedTask;
        }
    };
});

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<DatabaseMigrationService>();
builder.Services.AddScoped<AttendanceCalculationService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Seed roles and default users on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AttendanceManagementSystem.Data.ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AttendanceManagementSystem.Data.DatabaseInitializer.EnsureCoreDataAsync(db, logger);
}

app.Run();