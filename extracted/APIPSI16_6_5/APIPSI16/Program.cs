using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;

var builder = WebApplication.CreateBuilder(args);

// ---- Kestrel URLs ----
var defaultUrls = "https://localhost:7263;http://localhost:5270";
var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? defaultUrls;
builder.WebHost.UseUrls(urls);

// ---- CORS policy ----
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMvcFrontend", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ---- Connection string ----
var defaultConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                  ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(defaultConn))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection not configured.");
}

// ---- Register DbContext (scoped) ----
// IMPORTANT: force EF Core to use migrations from THIS project/assembly.
// This prevents EF from accidentally picking up a different migrations assembly.
builder.Services.AddDbContext<xcleratesystemslinks_SampleDBContext>(options =>
{
    options.UseSqlServer(defaultConn, sql =>
    {
        sql.MigrationsAssembly(typeof(Program).Assembly.FullName);
        // Optional: increase command timeout during migration
        // sql.CommandTimeout(120);
    });
});

// ---- Controllers & Swagger ----
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "APIPSI16 API", Version = "v1" });

    c.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });

    c.OperationFilter<APIPSI16.Filters.FileUploadOperation>();

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {token}"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ---- JWT Configuration ----
var keyBase64 = Environment.GetEnvironmentVariable("XCELERATE_JWT_KEY")
               ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(keyBase64))
{
    throw new InvalidOperationException("JWT key not configured. Set XCELERATE_JWT_KEY environment variable or Jwt:Key in appsettings.");
}

byte[] keyBytes;
try
{
    keyBytes = Convert.FromBase64String(keyBase64);
}
catch (FormatException)
{
    throw new InvalidOperationException("JWT key is not valid base64. Check XCELERATE_JWT_KEY or Jwt:Key value.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "xcelerate-links-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "xcelerate-links-clients";

// ---- Register services ----
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISessionService, SessionService>();

// ---- Authentication (JWT Bearer) ----
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction();
    options.SaveToken = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,

        ValidateAudience = true,
        ValidAudience = jwtAudience,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                logger.LogDebug("JWT OnMessageReceived. Authorization header present: {HasBearer}", authHeader.StartsWith("Bearer "));
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var userId = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var userRole = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            logger.LogInformation("JWT validated. User: {UserName} (ID: {UserId}), Role: {Role}", userName, userId, userRole ?? "NONE");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ctx.Exception, "JWT authentication failed: {Message}", ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnChallenge = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT challenge triggered. Error: {Error}, Description: {Description}", ctx.Error ?? "none", ctx.ErrorDescription ?? "none");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ---- Build the app ----
var app = builder.Build();

// ---- Startup diagnostics ----
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("=== JWT Configuration ===");
        logger.LogInformation("Key source: {Source}",
            Environment.GetEnvironmentVariable("XCELERATE_JWT_KEY") != null ? "Environment variable" : "appsettings.json");
        logger.LogInformation("Key length: {Length} bytes", keyBytes.Length);
        logger.LogInformation("Issuer: {Issuer}", jwtIssuer);
        logger.LogInformation("Audience: {Audience}", jwtAudience);

        logger.LogInformation("=== CORS Configuration ===");
        if (corsOrigins.Length > 0)
        {
            logger.LogInformation("Allowed origins: {Origins}", string.Join(", ", corsOrigins));
        }
        else
        {
            logger.LogInformation("CORS: Allow all origins (development mode)");
        }

        logger.LogInformation("=== Database Configuration ===");
        logger.LogInformation("Connection source: {Source}",
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") != null ? "Environment variable" : "appsettings.json");

        var ctx = scope.ServiceProvider.GetRequiredService<xcleratesystemslinks_SampleDBContext>();
        _ = ctx.Model;
        logger.LogInformation("DbContext resolved successfully.");

        // Log migrations state so you can see exactly what EF thinks it should do.
        var applied = ctx.Database.GetAppliedMigrations().ToList();
        var pending = ctx.Database.GetPendingMigrations().ToList();
        logger.LogInformation("EF migrations applied: {Count}", applied.Count);
        logger.LogInformation("EF migrations pending: {Count}", pending.Count);
        if (pending.Count > 0)
        {
            logger.LogWarning("Pending migrations: {Pending}", string.Join(", ", pending));
        }

        // Apply pending migrations automatically at startup.
        // NOTE: If your DB was created manually / has drift, this can throw.
        // Keep this enabled only if you want EF to manage schema.
        try
        {
            // If you want to avoid the Companies conflict while debugging, comment this out temporarily.
            ctx.Database.Migrate();

            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception migEx)
        {
            logger.LogError(migEx, "Failed to apply database migrations. The application will continue but may have schema issues.");
        }

        logger.LogInformation("=== Startup complete ===");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Startup validation failed.");
        throw;
    }
}

// ---- Middleware pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "APIPSI16 API v1");
        c.DocumentTitle = "APIPSI16 - Swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowMvcFrontend");

app.UseAuthentication();
app.UseAuthorization();

// REMOVE: app.UseMiddleware<SessionValidationMiddleware>();

app.MapControllers();
app.MapHub<APIPSI16.Hubs.ChatHub>("/hubs/chat");

app.Logger.LogInformation("API is running. Listening on: {Urls}", urls);

app.Run();