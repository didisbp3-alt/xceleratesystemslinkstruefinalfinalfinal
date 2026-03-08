using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using XcelerateLinks.Mvc.Services;             // TokenHandler, ApiClient, IApiClient, UsersApiClient
using XcelerateLinks.Models.ViewModels;
using XcelerateLinks.Mvc.Http;
using APIPSI16.Data;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TokenHandler>();

var mvcConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=sql.bsite.net\\MSSQL2016;Database=xcleratesystemslinks_SampleDB;User Id=xcleratesystemslinks_SampleDB;Password=XcelerateDB;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

builder.Services.AddDbContext<xcleratesystemslinks_SampleDBContext>(options =>
    options.UseSqlServer(mvcConnStr));

builder.Services.AddScoped<ISessionService, SessionService>();

// ---- Add services for password reset ----
builder.Services.AddSingleton<PasswordResetService>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddScoped<IUserService, UserService>();
// ----------------------------------------

var apiBase = Environment.GetEnvironmentVariable("Api__BaseUrl")
              ?? builder.Configuration["Api:BaseUrl"]
              ?? throw new InvalidOperationException("Api:BaseUrl not configured in appsettings.json or environment variable Api__BaseUrl.");

builder.Logging.AddConsole();
Console.WriteLine($"[MVC] Api BaseUrl = {apiBase}");

if (!apiBase.EndsWith('/')) apiBase += '/';

HttpMessageHandler CreatePrimaryHandler()
{
    if (builder.Environment.IsDevelopment())
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    }
    return new HttpClientHandler();
}

builder.Services.AddTransient<LoggingHandler>();

// Register named HttpClient "Api" with TokenHandler
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<TokenHandler>()
.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);

// Register typed API client for generic calls
builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<TokenHandler>()
.ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);

// Register UsersApiClient as scoped (correct for your constructor!)
builder.Services.AddScoped<IUsersApiClient, UsersApiClient>();

// Cookie authentication for MVC site
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.Cookie.Name = ".AspNetCore.Authentication.Cookies";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<IFileStorageService, FileStorageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

Console.WriteLine($"[MVC] Api BaseUrl = {apiBase}");

app.Run();