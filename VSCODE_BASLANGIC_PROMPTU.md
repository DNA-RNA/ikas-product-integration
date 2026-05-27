# VS Code'da Kullanacağın Başlangıç Prompt'u

Aşağıdaki prompt'u **Cline** veya **Continue** eklentisine yapıştır.

## Hazırlık (önce şunları yap)

1. Proje kökünde boş bir klasör aç (örn. `MultiSiteIkas/`).
2. `.claude/` klasörünü ve `docs/` klasörünü oraya kopyala. Yapı şöyle olmalı:
   ```
   MultiSiteIkas/
   ├── .claude/
   │   ├── state.md
   │   ├── agents/  (7 dosya)
   │   └── skills/  (6 dosya)
   └── docs/
       └── MULTI_SITE_IKAS_PROJECT_GUIDE.md   ← referans doküman
   ```
3. VS Code'da bu klasörü aç.
4. Cline veya Continue eklentisini kur, Claude Sonnet modelini seç.

---

## 🎯 KICK-OFF PROMPT (ilk mesaj)

```
Merhaba. Bu projede Multi-Site İkas Ürün Dağıtım Sistemi geliştireceğiz. C# / .NET 8 kullanacağız.

ÖNCE ŞUNU YAP:
1. Proje kökündeki `.claude/state.md` dosyasını oku. Projenin amacını, mağaza haritasını, teknoloji yığınını ve mevcut durumunu burada bulacaksın.
2. `docs/MULTI_SITE_IKAS_PROJECT_GUIDE.md` dosyasının İÇİNDEKİLER kısmını oku (sadece başlıkları). Bu, projenin tam teknik referansı. Detaylı kodu ihtiyaç oldukça okuyacaksın.
3. `.claude/agents/` klasöründeki tüm `.md` dosyalarının isimlerini listele — hangi uzman rolleri olduğunu göreceksin.
4. `.claude/skills/` klasöründeki tüm `.md` dosyalarının isimlerini listele.

ÇALIŞMA KURALLARIN:
- Yeni bir iş yapmaya başlamadan önce, hangi agent rolüne büründüğünü söyle ve o agent'ın .md dosyasını oku. Örn: "Şimdi `database-uzmani` olarak çalışıyorum, agent dosyasını okuyorum."
- Agent dosyasında referans verilen skill'leri lazım oldukça oku — hepsini bir defada yükleme.
- Yapılan her önemli adımı `state.md`'nin "İlerleme Durumu" listesindeki checkbox'ı işaretle.
- Açık sorulara cevap geldikçe `state.md`'deki "Açık Sorular" bölümünü güncelle.
- Kod yazarken `.claude/agents/csharp-pro.md` içindeki kurallara uy (nullable, async, structured logging vs.).
- Tek seferde çok büyük adım atma. Her büyük adımdan önce kısa bir plan sun, ben onaylayınca uygula.
- Referans dokümandaki kod örnekleri yol gösterici, ama yapı/agent kuralları daha bağlayıcı.

ŞİMDİ İLK ADIM:
1. state.md'yi oku ve **kısaca özetle**: ne yapacağımızı, hangi teknolojileri kullanacağımızı anladığını göster.
2. state.md'deki "Açık Sorular / Karar Bekleyenler" bölümünü oku, **bana o soruları sırayla sor**.
3. Cevaplarımı aldıkça state.md'yi güncelle.
4. Hepsini cevapladıktan sonra, `senkron-mimari` agent rolüne geç ve **Solution iskeletini oluşturmak için bana adım adım plan sun**. Henüz KOD YAZMA, sadece plan: hangi projeleri yaratacağız, hangi NuGet paketleri, hangi referanslar.

Hadi başla.
```

---

## 📋 SONRAKİ ADIMLARDA KULLANACAĞIN PROMPT'LAR

Kick-off bittikten sonra, sırayla şu prompt'larla ilerleyeceksin. Her birinden önce Claude'un önceki adımı tamamladığından emin ol.

### Adım 2 — Solution iskeleti
```
Plan onaylandı. Şimdi gerçekten dosyaları oluştur:
- Solution + 5 csproj (API, Core, Data, Jobs, Tests)
- Proje referansları (referans dokümandaki gibi)
- NuGet paketleri (her projeye)
- .gitignore (.NET için standard)
- global.json (.NET 8 SDK sabit)
- README.md (kısa)

csharp-pro agent kurallarına uy. Henüz hiçbir entity, controller veya service yazma — sadece iskelet.
state.md "İlerleme Durumu"nu güncelle.
```

### Adım 3 — Database katmanı
```
database-uzmani rolüne geç. skills/database-schema.md'yi oku.

MultiSiteIkas.Data projesinde:
1. Entity sınıflarını oluştur (Company, XmlSource, SiteMapping, Product, ProductTransfer, TransferLog) — Entities/ altında
2. MultiSiteDbContext'i oluştur — DbContext/ altında
3. Her entity için IEntityTypeConfiguration sınıfı — Configurations/ altında
4. Repository interface'leri — Interfaces/ altında
5. Henüz implementasyon yazma, sadece interface'ler
6. appsettings.json'a connection string placeholder ekle
7. Program.cs'ye DbContext registration ekle

Sonra ilk migration'ı oluştur: `dotnet ef migrations add InitialCreate`. Bu migration'ı uygulamak için bana komut ver.
```

### Adım 4 — XML Parser
```
xml-isleyici rolüne geç. skills/xml-feed-format.md'yi oku.

MultiSiteIkas.Core projesinde:
1. ParsedProduct DTO
2. IXmlParsingService interface
3. XmlParsingService implementation (XDocument ile)
4. XmlValidationException

MultiSiteIkas.Data projesinde:
1. IProductRepository implementation — UpsertAsync method
2. Bulk insert için EFCore.BulkExtensions ekle

Test data: MultiSiteIkas.Tests/TestData/sample_products.xml — 5 ürünlük örnek
Sonra unit test yaz: XmlParsingServiceTests.
```

### Adım 5 — Mapping servisleri
```
mapping-uzmani rolüne geç. skills/category-filtering.md ve skills/ikas-product-model.md'yi oku.

MultiSiteIkas.Core/Services/ altında:
1. CategoryFilterService + interface + birim testleri (8+ test senaryosu)
2. PricingService + interface + birim testleri
3. IkasFieldMapper + interface — henüz Category resolver entegrasyonu yok, mock'la
4. CategoryResolver interface — implementation Adım 6'da

Unit test öncelik 1, mantık karmaşık.
```

### Adım 6 — İkas GraphQL API client
```
ikas-api-uzmani rolüne geç. skills/ikas-graphql.md'yi oku.

MultiSiteIkas.Core/Services/ altında:
1. GraphQLRequest, GraphQLResponse internal class'ları
2. IkasCredentials record
3. IkasProductInput ve tüm alt class'ları (variant, price, stock, image vs.)
4. IIkasApiService interface — 4 metod (FindBySku, SaveProduct, ListCategories, ListBrands)
5. IkasApiService implementation
6. CategoryResolver implementation (cache'li, IMemoryCache)

Program.cs'ye HttpClient registration + Polly policies ekle (skills/error-handling-retry.md).
Integration test'leri Postman/manuel test sonrası ekleyeceğiz, şimdilik unit test yeterli.
```

### Adım 7 — Transfer Service (orkestratör)
```
transfer-orkestratoru rolüne geç. Tüm önceki servisleri kullanacaksın.

MultiSiteIkas.Core/Services/ altında:
1. TransferResult ve TransferError record'ları
2. ITransferService interface
3. TransferService implementation — agent dosyasındaki akışa BİREBİR uy:
   - SiteMapping al → log başlat → ürünleri filtrele → her birini transfer et (paralel max 5) → log bitir
4. Hata yönetimi: agent dosyasındaki katmanlara göre

Unit test: mock'lı senaryolar (success, partial failure, total failure, auth error abort).
```

### Adım 8 — Hangfire ve Job'lar
```
hangfire-uzmani rolüne geç. skills/hangfire-setup.md'yi oku.

1. Program.cs: Hangfire + SqlServer storage + HangfireServer registration
2. HangfireDashboardAuthFilter
3. app.UseHangfireDashboard("/hangfire", ...) — auth filter ile
4. JobScheduleOptions class
5. MultiSiteIkas.Jobs/Jobs/:
   - XmlPullJob
   - TransferJob
   - HealthCheckJob
6. Program.cs sonunda RecurringJobManager ile registration
7. appsettings.json'a JobSchedule bölümü

dotnet run sonrası /hangfire dashboard'a localhost'tan giriş olabilmeli.
```

### Adım 9 — REST API Controller'lar
```
csharp-pro + senkron-mimari rollerini birlikte kullan.

MultiSiteIkas.API/Controllers/ altında:
1. XmlSourceController — GET, POST, PUT, DELETE
2. SiteMappingController — GET, POST, PUT, DELETE + bulk filters update
3. JobsController — POST trigger endpoint'leri (admin için)
4. DashboardController — istatistikler (toplam ürün, son transfer durumları, error oranı)

FluentValidation ile request validation.
Swagger UI ekli (zaten template'te geliyor).
Authentication: şimdilik gerek yok, internal kullanım için. Production'da JWT eklenecek.
```

### Adım 10 — Docker Compose ve Production Hazırlığı
```
csharp-pro rolünde dön. 

1. Dockerfile (multi-stage build, .NET 8 SDK + ASP.NET runtime)
2. docker-compose.yml: api, mssql, seq (logging)
3. appsettings.Production.json template (env var placeholder'ları)
4. README.md güncelle: setup, run, deploy
5. CI/CD için GitHub Actions skeleton (build + test)

Production checklist'i state.md'ye ekle.
```

---

## 💡 İPUÇLARI

1. **Her oturumun başında state.md'yi Claude'a okutmakla başla.** Cline session'lar arası hafıza tutmaz — `state.md` projenin bellek deposu.

2. **Agent rolünü açıkça belirt.** "Şimdi xml-isleyici olarak çalış" demek, Claude'un sadece o agent'ın .md dosyasını ve referans verilen skill'leri okumasını sağlar — token tasarrufu.

3. **Küçük adımlarla ilerle.** "Tüm sistemi yaz" deme — yukarıdaki 10 adım gibi parçala.

4. **Onay isteyerek ilerle.** Cline'da auto-approve modunda dikkatli ol; her büyük adımdan önce plan iste, onayla, sonra uygulasın.

5. **state.md'yi sen de elle güncelle.** Mağaza credential'larını, gerçek XML URL'ini, kategori isimlerini kendin yazabilirsin — Claude bunları bilemez.

6. **Skill dosyaları yaşayan dokümanlar.** İkas API'sinin gerçek davranışını gördükçe `skills/ikas-graphql.md`'yi güncelle. Sonraki Claude oturumları daha doğru çalışır.

7. **Referans dokümana güven, agent kurallarına uy.** Çelişki olursa: agent kuralları (Clean Architecture, naming, async/await) > referans dokümandaki kod örnekleri. Doküman bazen kısa yol yapıyor, agent'lar production-ready yaklaşıyor.

8. **Secret'lar:** Asla appsettings.json'a İkas API Key/Secret yazma. User Secrets veya environment variable kullan.
   ```bash
   cd MultiSiteIkas.API
   dotnet user-secrets init
   dotnet user-secrets set "TestStoreApiKey" "sk_live_..."
   ```

---

## 🔥 KOPYALA-YAPIŞTIR HIZLI BAŞLANGIÇ

Eğer adım adım gitmek çok yavaş geliyorsa, yukarıdaki kick-off prompt'tan sonra şunu da deneyebilirsin (ama sonuçları kontrol et — Claude büyük adımları daha hatalı yapar):

```
Açık sorulara cevaplar:
- Sync sıklığı: 24 saat
- Master'da silinen ürün hedefte: deaktive et
- mallofmolds için: manuel çeviri kolonu (Faz 2)
- Görseller: URL referansı (basit yöntem)
- Rate limit: 60/dk varsayalım, gerçekte ölçeceğiz

state.md'yi güncelle, sonra Adım 2'den başla: Solution iskeleti + Adım 3: Database katmanı. İkisini birden tamamla, bana ara rapor sun.
```
