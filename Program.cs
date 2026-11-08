using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Authorization;
using PharmacySystem.Data;
using PharmacySystem.Resources;
using PharmacySystem.Services;
using PharmacySystem.Services.Auth;
using PharmacySystem.Services.Dashboard;
using PharmacySystem.Services.Email;
using PharmacySystem.Services.Notifications;
using PharmacySystem.Services.Packaging;

var builder = WebApplication.CreateBuilder(args);

// 1. Localization Services
builder.Services.AddLocalization();

// 2. EF Core DbContext with SQL Server provider
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection ")
    ?? "Server=.\\SQLEXPRESS;Database=PharmacyDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<PharmacyDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 3. HttpContext Accessor
builder.Services.AddHttpContextAccessor();

// 4. Domain & Business Services
builder.Services.AddScoped<IProductPackagingService, ProductPackagingService>();
builder.Services.AddScoped<IFefoAllocationService, FefoAllocationService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISaleTransactionService, SaleTransactionService>();
builder.Services.AddScoped<IPurchaseTransactionService, PurchaseTransactionService>();
builder.Services.AddScoped<IReturnTransactionService, ReturnTransactionService>();

// 4b. Configure EmailSettings with Options Pattern
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// 5. Authentication, Authorization & Identity Services
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// 6. Application Foundation Services (Notifications, Dashboard)
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 6b. Rate Limiting for Login and Password Reset
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login rate limit: 10 attempts per minute per IP
    options.AddPolicy("LoginLimiter", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_client",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Forgot password rate limit: 5 requests per 10 minutes per IP
    options.AddPolicy("PasswordRecoveryLimiter", httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_client",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
});

// 7. Cookie Authentication & Authorization Policies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events.OnValidatePrincipal = context =>
        {
            if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity && identity.IsAuthenticated)
            {
                if (identity.HasClaim(System.Security.Claims.ClaimTypes.Role, AppRoles.Developer) && !identity.HasClaim(System.Security.Claims.ClaimTypes.Role, AppRoles.Admin))
                {
                    identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, AppRoles.Admin));
                }
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, PharmacySystem.Services.Auth.PharmacyRoleClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.RequireDeveloper, policy =>
        policy.RequireRole(AppRoles.Developer, AppRoles.Admin));

    options.AddPolicy(AppPolicies.RequireAdmin, policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Developer));

    options.AddPolicy(AppPolicies.RequirePharmacistOrAdmin, policy =>
        policy.RequireRole(AppRoles.Admin, AppRoles.Pharmacist, AppRoles.Developer));
});

// 7b. Antiforgery Header Configuration for AJAX / POS JSON requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// 8. MVC Controllers & Views with Localization, AutoValidateAntiforgeryToken, and JSON String Enum support
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
        options.ModelBinderProviders.Insert(0, new PharmacySystem.ModelBinding.FlexibleDecimalModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    });

var app = builder.Build();

// Seed initial default users if database is available
await DbInitializer.SeedDefaultUsersAsync(app.Services);

// 9. Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 10. Request Localization (Arabic as primary default, English supported)
var arCulture = new System.Globalization.CultureInfo("ar");
arCulture.NumberFormat.NumberDecimalSeparator = ".";
arCulture.NumberFormat.CurrencyDecimalSeparator = ".";

var enCulture = new System.Globalization.CultureInfo("en");
enCulture.NumberFormat.NumberDecimalSeparator = ".";
enCulture.NumberFormat.CurrencyDecimalSeparator = ".";

var supportedCultures = new[] { arCulture, enCulture };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures("ar", "en")
    .AddSupportedUICultures("ar", "en");

localizationOptions.SupportedCultures = supportedCultures;
localizationOptions.SupportedUICultures = supportedCultures;

localizationOptions.RequestCultureProviders = new IRequestCultureProvider[]
{
    new CookieRequestCultureProvider(),
    new QueryStringRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};

app.UseRequestLocalization(localizationOptions);

app.UseRouting();

// 10b. Rate Limiter Middleware
app.UseRateLimiter();

// 11. Authentication & Authorization Middlewares
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
