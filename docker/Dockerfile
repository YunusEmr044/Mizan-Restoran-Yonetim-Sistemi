# Mizan uygulama imajı. Derleme bağlamı depo köküdür:
#   docker build -f docker/Dockerfile -t mizan .
# (docker/docker-compose.yml bunu kendisi yapar.)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyası: bağımlılıklar değişmedikçe restore katmanı önbellekten gelir.
COPY RestoranYonetim.csproj ./
RUN dotnet restore RestoranYonetim.csproj -r linux-x64

COPY . ./
# Yalnızca linux-x64: diğer platformların yerel kütüphaneleri (Windows/macOS/ARM, ~470 MB) imaja girmez.
RUN dotnet publish RestoranYonetim.csproj -c Release -r linux-x64 --self-contained false -o /app/publish --no-restore /p:UseAppHost=false

# Çalışma imajı: yalnızca ASP.NET çalışma ortamı (Debian; Türkçe kültür için ICU ve saat dilimi verisi içerir).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# GitHub'da paket bu etiketle depoya bağlanır (depo sayfasında "Packages" altında görünür).
LABEL org.opencontainers.image.source="https://github.com/YunusEmr044/Mizan-Restoran-Yonetim-Sistemi"       org.opencontainers.image.title="Mizan"       org.opencontainers.image.description="Mizan Restoran Yönetim Sistemi"       org.opencontainers.image.licenses="MIT"

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    TZ=Europe/Istanbul \
    Kestrel__Endpoints__Http__Url=http://+:8080

COPY --from=build /app/publish ./

# Kalıcı veriler birimlere bağlanır; klasörler önceden oluşturulup uygulama kullanıcısına verilir ki
# birim ilk bağlandığında doğru sahiplikle başlasın. Yedek klasörü SQL Server konteynerinde de aynı yoldadır.
RUN mkdir -p /app/App_Data/keys /app/wwwroot/uploads /var/opt/mssql/backup \
    && chown -R $APP_UID:$APP_UID /app/App_Data /app/wwwroot/uploads /var/opt/mssql/backup

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RestoranYonetim.dll"]
