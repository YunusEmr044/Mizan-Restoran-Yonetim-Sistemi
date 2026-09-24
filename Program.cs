using System.IO.Compression;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.HealthChecks;
using RestoranYonetim.Hubs;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

var builder = WebApplication.CreateBuilder(args);

// Windows Service olarak kurulduğunda (bkz. README.md "Windows Service") otomatik devreye girer;
// interaktif/konsol/dotnet run olarak çalışırken etkisiz - mevcut geliştirme akışını bozmaz.
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "RestoranYonetim";
});

// GÜVENLİK: appsettings.json gerçek bir bağlantı dizesi içermez (production secret source code'da olmamalı) - production'da SADECE ConnectionStrings__DefaultConnection ortam değişkeni ya da user-secrets ile gelir. Eksikse "fail fast" ile açık hatayla başlangıçta çöker.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' bulunamadı. appsettings.Development.json (yerel geliştirme) " +
        "veya ConnectionStrings__DefaultConnection ortam değişkeni / 'dotnet user-secrets' (production) " +
        "üzerinden ayarlanmalı - bkz. README.md \"Production Kurulumu\".");
}

// GÜVENLİK: PublicBaseUrl, QR/rezervasyon linklerinde Request.Host (sahteleşebilir bir HTTP başlığı) yerine kullanılır. Yönetim panelinden (Ayarlar → Site Adresi) de girilebilir, panel değeri önceliklidir (bkz. Services/PublicUrl.cs).
var publicBaseUrl = builder.Configuration["PublicBaseUrl"];

// Varsayılan (gerçek/internete açık production) true: HTTPS zorunlu + HSTS. Sadece aynı bina/WiFi
// LAN'ında (genel internete açılmayan) geçerli bir HTTPS sertifikası olmadan çalıştırılan kurulumlar
// için appsettings.Production.json/ortam değişkeniyle false yapılabilir - bkz. README.md
// "Tek Makinede Yayına Alma".
var requireHttps = builder.Configuration.GetValue("RequireHttps", true);

// AddDbContextPool: context'leri havuzdan ödünç alıp geri verir (yoğun trafikte GC maliyetini azaltır). EnableRetryOnFailure geçici bağlantı kopmalarında sorguyu otomatik yeniden dener. SlowQueryInterceptor yavaş sorguları/deadlock'ları loglar.
builder.Services.AddDbContextPool<ApplicationDbContext>((serviceProvider, options) =>
    options.UseSqlServer(connectionString, sqlOptions =>
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null))
        .AddInterceptors(new SlowQueryInterceptor(serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<SlowQueryInterceptor>())));

// Periyodik otomatik veritabanı yedeği (bkz. Services/DatabaseBackupService.cs, appsettings.json "DatabaseBackup").
builder.Services.AddHostedService<DatabaseBackupService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Parola politikası: min 8 karakter + rakam + harf. Tam kurumsal karmaşıklık personelin günlük kullanımını zorlaştırmasın diye kasıtlı istenmedi.
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;

        // Art arda başarısız giriş denemelerinde hesabı geçici kilitler (brute-force savunması).
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, PermissionClaimsTransformation>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PermissionCacheVersion>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/admin";
    options.AccessDeniedPath = "/admin";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    // GÜVENLİK: oturum çerezi JS'ten erişilemez; HTTPS zorunluysa sadece HTTPS'te gönderilir. RequireHttps=false
    // (sertifikasız LAN kurulumu) iken Always kalırsa tarayıcı çerezi http://192.168.x.x'te reddeder ve tabletlerden giriş yapılamaz.
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddControllersWithViews();

// GÜVENLİK NOTU: HTTPS'te yanıt sıkıştırma varsayılan olarak kapalıdır (BREACH saldırısı riski) - bu projede token içeren yansıyan içerik olmadığından bilinçli olarak açıldı.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddSignalR();
builder.Services.AddScoped<RealtimeNotifier>();

// Her izin kodu (Permissions.*) için otomatik bir authorization policy kurar - [Authorize(Policy = Permissions.X.Y)] bu sayede çalışır.
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
        {
            if (permission == Permissions.Reports.View)
            {
                policy.RequireAssertion(context =>
                    context.User.HasClaim(Permissions.ClaimType, permission) ||
                    context.User.IsInRole(RolePermissions.SuperAdmin) ||
                    context.User.IsInRole(RolePermissions.RestoranSahibi) ||
                    context.User.IsInRole(RolePermissions.Mudur) ||
                    context.User.IsInRole(RolePermissions.Muhasebe) ||
                    context.User.IsInRole(RolePermissions.RaporGoruntuleyici));
                return;
            }

            policy.RequireClaim(Permissions.ClaimType, permission);
        });
    }
});

// GÜVENLİK: ters proxı arkasında çalışırken gerçek istemci IP'si/şeması X-Forwarded-For/Proto başlıklarında gelir - bu middleware olmadan HTTPS yönlendirme ve IP bazlı hız sınırlaması yanlış çalışır (tüm istekler proxy'nin tek IP'sine düşer). Varsayılan olarak SADECE loopback'ten gelen bu başlıklara güvenilir; farklı makinedeki bir proxy için appsettings.json "ReverseProxy:KnownProxies" altına gerçek IP eklenmeli, aksi halde başlıklar yok sayılır (fail-safe).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var ip))
        {
            options.KnownProxies.Add(ip);
        }
    }
});

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<AdisyonService>();

// /health: bir izleme aracının uygulamanın ayakta olup olmadığını ve DB'ye erişebildiğini görmesi için.
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// Varsayılan MultipartBodyLengthLimit (128MB) çok gevşek - burada 10MB üst sınır konarak aşırı büyük gövdelerle bellek/bant tüketilmesi (basit bir DoS vektörü) engellenir.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

// Kimlik doğrulama gerektirmeyen uç noktalarda IP bazlı hız sınırlaması (brute-force/bot/DoS savunması) - sınır aşılınca doğrudan 429 döner.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("public-endpoints", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Config validation (fail fast): production'da kritik ayarlar eksikse uygulama açık bir hatayla başlangıçta durur.
if (!app.Environment.IsDevelopment())
{
    var startupErrors = new List<string>();
    if (!string.IsNullOrWhiteSpace(publicBaseUrl) && !PublicUrl.TryNormalize(publicBaseUrl, out _))
    {
        startupErrors.Add("PublicBaseUrl config değeri geçersiz (http:// veya https:// ile başlayan mutlak bir adres olmalı, ör. \"https://restoranadi.com\").");
    }
    else if (string.IsNullOrWhiteSpace(publicBaseUrl))
    {
        app.Logger.LogWarning(
            "PublicBaseUrl ayar dosyasında yok - yönetim panelinde Ayarlar → Site Adresi'nden girilmezse " +
            "QR kodları ve site linkleri isteğin kendi adresini kullanır.");
    }

    if (startupErrors.Count > 0)
    {
        throw new InvalidOperationException(
            "Production başlangıç kontrolü başarısız - uygulama güvensiz bir config ile başlatılmıyor:\n - " +
            string.Join("\n - ", startupErrors) + "\nBkz. README.md \"Production Kurulumu\".");
    }

    if (builder.Configuration["AllowedHosts"] == "*")
    {
        // Fail fast değil, sadece uyarı - Host header injection riskini azaltmak için gerçek domain(ler)e sınırlandırılmalı.
        app.Logger.LogWarning(
            "AllowedHosts hâlâ \"*\" (sınırsız) - production'da gerçek domain(ler)inize " +
            "sınırlandırmanız önerilir (appsettings.json \"AllowedHosts\").");
    }
}

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// Ters proxy başlıkları pipeline'ın en başına uygulanır - sonraki tüm middleware'ler gerçek istemci IP'sini/şemasını görür.
app.UseForwardedHeaders();

// Her isteğe TraceIdentifier'ı yanıt header'ına (X-Correlation-ID) ekler ve o istek süresince atılan tüm log satırlarına iliştirir - üretimde bir hatayı loglardan tek kimlikle bulabilmek için.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestCorrelation");
    using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = context.TraceIdentifier }))
    {
        await next();
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (requireHttps)
    {
        app.UseHsts();
    }
}

// UseExceptionHandler sadece 500'leri yakalar - NotFound()/Forbid() gibi olağan HTTP hataları da aynı markalı Hata sayfasıyla gösterilsin diye.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

// Güvenlik başlıkları (tüm yanıtlara) - clickjacking/MIME-sniffing/XSS etkisini azaltan tarayıcı savunma katmanları. CSP script-src/style-src 'unsafe-inline' içeriyor çünkü görünümlerde inline <script>/style="" kullanılıyor; nonce tabanlı CSP'ye geçiş ileri bir aşamaya bırakıldı.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
            "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
            "img-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-src https://www.google.com; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'self';";
        return Task.CompletedTask;
    });

    await next();
});

app.UseResponseCompression();

if (requireHttps)
{
    app.UseHttpsRedirection();
}

// Statik dosyalara 7 günlük tarayıcı önbellekleme başlığı eklenir - asp-append-version içerik değiştiğinde URL'i değiştirip önbelleği zaten geçersiz kılıyor.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800";
    }
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<OpsHub>("/hubs/ops");

app.MapHealthChecks("/health");

// "/admin" artık ayrı bir dashboard kısayolu değil - AccountController.Login bu URL'i devralıyor
// (giriş yapılmamışsa giriş formu, girişliyse role göre ilgili ekrana yönlendirme). Bkz. AccountController.cs.

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
