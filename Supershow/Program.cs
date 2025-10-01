global using Supershow.Models;
global using Supershow.Services;
global using Supershow.Hubs;
global using X.PagedList.Extensions;
using Supershow.BackgroundWorkers;
using Supershow.Middlewares;
using Stripe;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new ResponseCacheAttribute
    {
        NoStore = true
    });
});

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\SuperShow.mdf;
");

// Add authentication
builder.Services.AddAuthentication().AddCookie("Cookies", options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Error/403";
});

// Define policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin Home", policy =>
    {
        policy.RequireRole("Super Admin", "Support Staff", "HR Manager", "Movie Manager", "F&B Manager", "Operation Manager", "Cinema Manager", "Inventory Manager", "Booking Manager", "F&B Order Manager", "Sales Analyst");
    });

    // Sales report
    options.AddPolicy("Sales Report", policy =>
    {
        policy.RequireRole("Super Admin", "Sales Analyst");
    });

    // Manage customers
    options.AddPolicy("Manage Customers", policy =>
    {
        policy.RequireRole("Super Admin", "Support Staff");
    });

    // Manage admins
    options.AddPolicy("Manage Admins", policy =>
    {
        policy.RequireRole("Super Admin", "HR Manager");
    });

    // Manage cinemas
    options.AddPolicy("Manage Cinemas", policy =>
    {
        policy.RequireRole("Super Admin", "Operation Manager", "Cinema Manager");
    });

    // Manage movies
    options.AddPolicy("Manage Movies", policy =>
    {
        policy.RequireRole("Super Admin", "Movie Manager");
    });

    // Manage showtimes
    options.AddPolicy("Manage Showtimes", policy =>
    {
        policy.RequireRole("Super Admin", "Operation Manager", "Cinema Manager");
    });

    // Manage F&B items
    options.AddPolicy("Manage F&B Items", policy =>
    {
        policy.RequireRole("Super Admin", "F&B Manager");
    });

    // Manage F&B inventory
    options.AddPolicy("Manage F&B Inventory", policy =>
    {
        policy.RequireRole("Super Admin", "Cinema Manager", "Inventory Manager");
    });

    // Manage bookings
    options.AddPolicy("Manage Bookings", policy =>
    {
        policy.RequireRole("Super Admin", "Support Staff", "Cinema Manager", "Booking Manager");
    });

    // Cancel bookings
    options.AddPolicy("Cancel Bookings", policy =>
    {
        policy.RequireRole("Customer", "Super Admin", "Support Staff", "Cinema Manager", "Booking Manager");
    });

    // Manage F&B orders
    options.AddPolicy("Manage F&B Orders", policy =>
    {
        policy.RequireRole("Super Admin", "Support Staff", "Cinema Manager", "F&B Order Manager");
    });

    // Cancel F&B orders
    options.AddPolicy("Cancel F&B Orders", policy =>
    {
        policy.RequireRole("Customer", "Super Admin", "Support Staff", "Cinema Manager", "F&B Order Manager");
    });

    // Manage experiences
    options.AddPolicy("Manage Experiences", policy =>
    {
        policy.RequireRole("Super Admin", "Operation Manager");
    });

    // Manage seat types
    options.AddPolicy("Manage Seat Types", policy =>
    {
        policy.RequireRole("Super Admin", "Operation Manager");
    });
});

// Add http context accessor
builder.Services.AddHttpContextAccessor();

// Add services
builder.Services.AddSingleton<ImageService>();

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<ExpiryCleanupService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<FnbOrderService>();
builder.Services.AddScoped<ShowtimeService>();

// Add background worker
builder.Services.AddHostedService<ExpiryCleanupBackgroundWorker>();

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseStatusCodePagesWithReExecute("/Error/{0}"); // hit error controller if error occurs

// Add middlewares
app.UseMiddleware<ExpiryCleanupMiddleware>();
app.UseMiddleware<AuthSessionMiddleware>();

// Add hubs
app.MapHub<BookingHub>("/BookingHub");
app.MapHub<FnbOrderHub>("/FnbOrderHub");
app.MapHub<AccountHub>("/AccountHub");

app.MapDefaultControllerRoute();
app.Run();