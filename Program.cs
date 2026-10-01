using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection string & Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<TruyenSongNguContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Khắc phục lỗi rớt kết nối mạng / transient failures khi chạy hosting
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Bộ đệm bộ nhớ (RAM) cần thiết cho RateLimitAttribute
builder.Services.AddMemoryCache();

// 2. Các Service ngoài
builder.Services.AddHttpClient<IAiTranslationService, GeminiTranslationService>();
builder.Services.AddTransient<IEmailSenderService, EmailSenderService>();
builder.Services.AddScoped<MangaTranslatorService>();
builder.Services.AddScoped<IParagraphAiProcessingService, ParagraphAiProcessingService>();

// 3. Quản lý Session & Cookie Policy
// Dùng RAM thay vì SQL Server để loại bỏ nghẽn I/O và tiết kiệm CPU database
builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".WebTruyenTranh.Session";
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 4. Cấu hình Hangfire an toàn (Tránh cạn kiệt Connection Pool và giảm tải CPU)
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(30), // Không để TimeSpan.Zero để tránh spam query
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // 1 luồng ngầm cho hosting để giải phóng tài nguyên
});

var app = builder.Build();

// Hỗ trợ nhận diện đúng IP thật khi chạy sau IIS Reverse Proxy / Cloudflare
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Thứ tự Middleware chuẩn
app.UseCookiePolicy();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Bật Hangfire Dashboard
app.UseHangfireDashboard("/hangfire");

// Area routes
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();