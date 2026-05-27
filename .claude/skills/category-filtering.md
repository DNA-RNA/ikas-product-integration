# Kategori Filtreleme — Detaylı Mantık

## Problem

Master mağazada (hobizubi.com) ~10K ürün var. Her hedef mağaza bunların sadece bir alt kümesini almalı:
- `recinem.com` → Reçine, Epoksi, Pigment kategorilerinden olanlar
- `boncukpasaji.com` → Boncuk, İp/Tel, Takı kategorileri
- `kalipatolyesi.com` → Silikon Kalıp, Reçine Kalıp
- `mallofmolds.com` → Silicone Molds, Resin Molds

Bu filtreleme `site_mappings.category_filters` JSON array kolonunda saklanır.

## JSON Format

```json
[
  "Hobi > Reçine",
  "Hobi > Epoksi > *",
  "Pigment"
]
```

Üç filtreleme tipi:

### 1. Tam Eşleşme (Exact Match)
Filter: `"Hobi > Reçine > Epoksi"`
- Product: `"Hobi > Reçine > Epoksi"` → ✅ match
- Product: `"Hobi > Reçine"` → ❌ no match
- Product: `"Hobi > Reçine > Epoksi > Şeffaf"` → ❌ no match (alt kategori değil)

### 2. Wildcard / Alt Kategori (`> *`)
Filter: `"Hobi > Reçine > *"`
- Product: `"Hobi > Reçine > Epoksi"` → ✅ match
- Product: `"Hobi > Reçine > Epoksi > Şeffaf"` → ✅ match
- Product: `"Hobi > Reçine"` → ❌ no match (kendi değil, sadece altları)
- Product: `"Hobi > Boncuk > Cam"` → ❌ no match

### 3. Substring Match (kategori adı path içinde geçiyor)
Filter: `"Reçine"`
- Product: `"Hobi > Reçine > Epoksi"` → ✅ match
- Product: `"Reçine Aksesuarları"` → ✅ match
- Product: `"Hobi > Boncuk"` → ❌ no match

> **Substring match** esnek ama tehlikeli — örneğin filter `"Boncuk"` ile `"Boncuklu Bilezik"` de match olur. Production'da çoğunlukla **tam eşleşme + wildcard** kullanılsın, substring son çare olsun.

## Implementation

```csharp
public interface ICategoryFilterService
{
    bool ShouldTransfer(string categoryPath, IReadOnlyList<string> filters);
}

public sealed class CategoryFilterService : ICategoryFilterService
{
    public bool ShouldTransfer(string categoryPath, IReadOnlyList<string> filters)
    {
        if (string.IsNullOrWhiteSpace(categoryPath)) return false;
        if (filters is null || filters.Count == 0) return false; // güvenli default: filtre yoksa transfer etme

        var normalizedPath = NormalizePath(categoryPath);

        foreach (var rawFilter in filters)
        {
            var filter = NormalizePath(rawFilter);

            // 1. Wildcard: "Hobi > Reçine > *"
            if (filter.EndsWith(" > *", StringComparison.Ordinal))
            {
                var prefix = filter[..^4]; // "Hobi > Reçine"
                if (normalizedPath.StartsWith(prefix + " > ", StringComparison.OrdinalIgnoreCase))
                    return true;
                continue;
            }

            // 2. Tam eşleşme
            if (string.Equals(normalizedPath, filter, StringComparison.OrdinalIgnoreCase))
                return true;

            // 3. Substring (path segmentlerinden biri tam eşleşiyorsa)
            var segments = normalizedPath.Split(" > ", StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => string.Equals(s.Trim(), filter, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static string NormalizePath(string path) =>
        path.Trim()
            .Replace("  ", " ")
            .Replace(">", " > ")
            .Replace("  >", " >")
            .Replace(">  ", "> ")
            .Replace("  ", " ");
}
```

## Edge Case'ler

| Senaryo | Beklenen |
|---------|----------|
| Filtre boş `[]` | Hiçbir ürün transfer edilmez |
| Path boş `""` | Transfer edilmez |
| Filtre `"*"` (sadece) | Tüm ürünleri transfer et (henüz desteklenmiyor, gerekirse ekle) |
| Path ve filtre büyük/küçük harf farkı | OrdinalIgnoreCase ile match |
| Fazla boşluk: `"Hobi  >  Reçine"` | Normalize edilir |
| Türkçe karakter | Direkt karşılaştırma, normalize edilmez (case sensitivity bu noktada zaten gevşek) |

## Test Senaryoları

```csharp
[Theory]
[InlineData("Hobi > Reçine > Epoksi", new[] { "Hobi > Reçine > Epoksi" }, true)]
[InlineData("Hobi > Reçine > Epoksi", new[] { "Hobi > Reçine > *" }, true)]
[InlineData("Hobi > Reçine > Epoksi > Şeffaf", new[] { "Hobi > Reçine > *" }, true)]
[InlineData("Hobi > Reçine", new[] { "Hobi > Reçine > *" }, false)] // wildcard kendini içermez
[InlineData("Hobi > Boncuk > Cam", new[] { "Hobi > Reçine > *" }, false)]
[InlineData("Hobi > Reçine > Epoksi", new[] { "Reçine" }, true)] // segment match
[InlineData("", new[] { "Reçine" }, false)]
[InlineData("Hobi > Reçine", new string[] { }, false)]
public void ShouldTransfer_VariousScenarios(string path, string[] filters, bool expected)
{
    var service = new CategoryFilterService();
    var result = service.ShouldTransfer(path, filters);
    result.Should().Be(expected);
}
```

## Admin UI Açısından

Admin filter setini yönetirken:
1. Aktif filter'ları liste olarak göster.
2. Ekleme: input + "Add" butonu, validate (boş değil).
3. Silme: her satırın yanında X butonu.
4. Preview: "Şu anki filtre setiyle X ürün transfer edilecek" sayısı — DB'den count sorgusu.

### Preview Query
```sql
SELECT COUNT(*)
FROM products p
WHERE p.xml_source_id = @xmlSourceId
  AND (
    -- Tam eşleşme örnek
    p.category_path = 'Hobi > Reçine > Epoksi'
    -- Wildcard örnek
    OR p.category_path LIKE 'Hobi > Reçine > %'
    -- Substring örnek
    OR p.category_path LIKE '%Reçine%'
  );
```

Bu SQL dinamik olarak C# tarafında oluşturulur. Daha temiz: tüm ürünleri çek + memory'de filter (10K ürünle problem yok).

## "Hangi kategoriler eşlenmedi?" Raporu

Admin için kritik bir rapor: master mağazada olup hiçbir hedef mağaza filtresine girmeyen kategoriler.

```sql
-- Master mağazadaki distinct kategoriler
SELECT DISTINCT category_path
FROM products
WHERE xml_source_id = @xmlSourceId
ORDER BY category_path;
```

Bu listeyi alıp her hedef mağaza filter'ı ile karşılaştır, hangisi hiçbir mağazaya gitmiyorsa admin'e göster. Admin görüp ya yeni filter eklesin ya kategori master'da yanlış mı diye baksın.

## Performans

10K ürün × 4 hedef mağaza = 40K filter check. Her check O(filter_count) = sabit. Toplam ~200K string comparison; memory'de saniyenin altında biter. Endişelenecek bir şey yok.
