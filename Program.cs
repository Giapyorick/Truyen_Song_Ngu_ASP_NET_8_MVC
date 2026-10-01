using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using WebTruyenTranh.Helpers;
using WebTruyenTranh.Models;
using WebTruyenTranh.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<TruyenSongNguContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// 2. Các Service ngoài
builder.Services.AddHttpClient<IAiTranslationService, GeminiTranslationService>();
builder.Services.AddTransient<WebTruyenTranh.Helpers.IEmailSenderService, WebTruyenTranh.Helpers.EmailSenderService>();
builder.Services.AddScoped<MangaTranslatorService>();
builder.Services.AddScoped<IParagraphAiProcessingService, ParagraphAiProcessingService>();

// 3. Distributed Cache (Lưu session vào Database SQL Server)
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "dbo";
    options.TableName = "TblSessionCache";
});

// 4. BỔ SUNG: Cấu hình Cookie Policy (Bắt buộc để lưu session ngay lần đầu)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // Bỏ qua kiểm tra chấp thuận cookie -> Cho phép tạo cookie session ngay lập tức
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

// 5. BỔ SUNG & HỢP NHẤT: Cấu hình Session (Chỉ khai báo 1 lần duy nhất)
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".WebTruyenTranh.Session";
    options.IdleTimeout = TimeSpan.FromHours(4); // Thời gian sống của session
    options.Cookie.HttpOnly = true;               // Chống tấn công XSS
    options.Cookie.IsEssential = true;           // Đánh dấu là cookie thiết yếu (không bị chặn)
    options.Cookie.SameSite = SameSiteMode.Lax;  // Cho phép chuyển hướng vẫn giữ session
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Tương thích cả HTTP lẫn HTTPS
});

// 6. Cấu hình Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// THỨ TỰ MIDDLEWARE BẮT BUỘC:
app.UseCookiePolicy(); // BẮT BUỘC: Phải gọi trước UseSession
app.UseSession();      // BẮT BUỘC: Phải gọi trước UseAuthorization
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