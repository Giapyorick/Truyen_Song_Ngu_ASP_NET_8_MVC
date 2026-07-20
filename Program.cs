using Microsoft.EntityFrameworkCore;
using WebTruyenTranh.Models;
using OfficeOpenXml;
using WebTruyenTranh.Helpers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TruyenSongNguContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<IAiTranslationService, GeminiTranslationService>();
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = "Data Source=DESKTOP-8T25JED;Initial Catalog=TruyenSongNgu;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    options.SchemaName = "dbo";
    options.TableName = "TblSessionCache";
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name : "areas",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
    );

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
