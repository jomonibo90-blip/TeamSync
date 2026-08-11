using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSync.BackgroundJobs;
using TeamSync.Data;
using TeamSync.Hubs;
using TeamSync.Models;
using TeamSync.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add Signal R services with CORS
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024 * 1024; // 32 MB
});

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials()
               .SetIsOriginAllowed(origin => true);
    });
});

// Add Entity Framework DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration. Please set it in appsettings.Production.json or App Service application settings.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Identity services
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add custom services
builder.Services.AddScoped<DbInitializerService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IDigestEmailService, DigestEmailService>();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Add Blob Storage service for file uploads
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Add background service for deadline checking
builder.Services.AddHostedService<DeadlineCheckService>();

// Add background service for weekly digest emails
builder.Services.AddHostedService<WeeklyDigestBackgroundService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializerService>();
    await dbInitializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<ChatHub>("/chatHub");

// Debug middleware: log 404s to help track down missing routes
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 404)
    {
        var logger = app.Logger;
        logger.LogWarning("404 Not Found for request {Method} {Path}{QueryString}", context.Request.Method, context.Request.Path, context.Request.QueryString);
    }
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();
