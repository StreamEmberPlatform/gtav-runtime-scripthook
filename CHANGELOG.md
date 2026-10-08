# Değişiklik günlüğü

Sürümler `VERSION` (major.minor) + commit sayısı (patch) ile otomatik verilir; her `main` push'u bir sürümdür.
Burada yalnız kayda değer değişiklikler tutulur.

## 1.0
- İlk StreamEmber dağıtımı: `StreamEmber.Runtime.GTAV.asi` + `StreamEmber.Scripting.GTAV.dll`, `StreamEmber\` klasör
  düzeni (Runtime, Scripts, Config, Logs, Manifests, Licenses), kendi sürüm numaraları.
- v2 API'si, `.pdb` ve `.xml` dosyaları dağıtımdan çıkarıldı.
- Çökme sertleştirmeleri (bkz. README, "Upstream'den farklarımız").
- GitHub Actions: derleme, testler, `main`'e her push'ta otomatik release.
