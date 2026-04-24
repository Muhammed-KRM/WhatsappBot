# WhatsApp Kafe Otomasyon - CafeBot

Bu proje, WhatsApp gruplarında paylaşılan vardiya mesajlarını otomatik olarak algılayıp, kullanıcının belirlediği öncelik sırasına göre uygun saati gruba yanıt olarak gönderen bir otomasyon sistemidir.

## Proje Yapısı

```
CafeBot/
├── CafeBot.Data/           # Data Layer - Entity Framework Core, Repositories
├── CafeBot.Business/       # Business Layer - Services, DTOs, External API Clients
├── CafeBot.API/           # Web API - REST endpoints, SignalR Hub
├── CafeBot.Worker/        # Background Worker - Message Listener & Processor
├── CafeBot.Web/           # Blazor Web UI - User Interface
└── CafeBot.sln            # Solution File
```

## Teknoloji Stack

- **.NET 8.0** - Runtime
- **Entity Framework Core 8.0.11** - ORM (SQLite)
- **ASP.NET Core Web API** - REST API
- **ASP.NET Core Worker Service** - Background Processing
- **Blazor Server** - Web UI
- **SignalR** - Real-time Communication
- **Evolution API** - WhatsApp Integration
- **Google Gemini API** - AI Message Parsing

## Proje Referansları

```
CafeBot.Data
    └── (No dependencies)

CafeBot.Business
    └── CafeBot.Data

CafeBot.API
    ├── CafeBot.Business
    └── CafeBot.Data

CafeBot.Worker
    ├── CafeBot.Business
    └── CafeBot.Data

CafeBot.Web
    ├── CafeBot.Business
    └── CafeBot.Data
```

## Konfigürasyon

### API & Worker (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=cafebot.db"
  },
  "EvolutionApi": {
    "BaseUrl": "http://localhost:8080",
    "ApiKey": "your-evolution-api-key-here"
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key-here",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta",
    "Model": "gemini-1.5-flash"
  }
}
```

### Web (appsettings.json)

```json
{
  "ApiBaseUrl": "http://localhost:5000"
}
```

## Kurulum

1. **Projeyi klonlayın:**
   ```bash
   cd D:\otomesaj\CafeBot
   ```

2. **NuGet paketlerini geri yükleyin:**
   ```bash
   dotnet restore
   ```

3. **Projeyi derleyin:**
   ```bash
   dotnet build
   ```

4. **API anahtarlarını yapılandırın:**
   - `CafeBot.API/appsettings.json` dosyasını düzenleyin
   - `CafeBot.Worker/appsettings.json` dosyasını düzenleyin
   - Evolution API ve Gemini API anahtarlarınızı ekleyin

## Çalıştırma

### Development Ortamı

Farklı terminal pencerelerinde:

```bash
# API
cd CafeBot.API
dotnet run

# Worker
cd CafeBot.Worker
dotnet run

# Web UI
cd CafeBot.Web
dotnet run
```

### Docker ile Çalıştırma

(Docker konfigürasyonu sonraki görevlerde eklenecek)

## Geliştirme Durumu

✅ **Task 1: Proje Yapısı ve Temel Konfigürasyon** - TAMAMLANDI
- Solution ve 5 proje oluşturuldu
- Proje referansları eklendi
- NuGet paketleri yüklendi (EF Core 8.0.11)
- appsettings.json dosyaları yapılandırıldı
- Proje başarıyla derlendi

⏳ **Task 2: Data Katmanı** - Beklemede
⏳ **Task 3: Business Katmanı Infrastructure** - Beklemede
⏳ **Task 4: Business Katmanı DTOs ve Interfaces** - Beklemede
⏳ **Task 5: Business Katmanı Services** - Beklemede

## Lisans

Bu proje özel kullanım içindir.
