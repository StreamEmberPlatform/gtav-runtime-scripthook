# Değişiklik günlüğü

Sürümler `VERSION` (major.minor) + commit sayısı (patch) ile otomatik verilir; her `main` push'u bir sürümdür.
Burada yalnız kayda değer değişiklikler tutulur.

## Yayımlanmamış — 2026-10-09
- Arka plan threadlerinden oyun çağrılarını reddetme, tick bütçesi ve sınırlı Live kuyruğu. Kod yamalarında native işlem: thread konumu/beklenen bayt kontrolü, başka modun yamasını ezmeden geri alma. Windows eşzamanlı yama testleri.
- F4 konsolu: klavye callback'inde oyun native'i çağırmama, unmanaged sınıra exception taşımama, güvenli metin tamponu,
  thread-safe ve sınırlı log geçmişi, korumalı çizim indeksleri/native dönüşleri ve clipboard/compiler hata yalıtımı.

## 1.2
- StreamEmber Live (`StreamEmber.Live`): canlı yayın modları için gömülü EventFabric/GCore bağlantısı, Falcon ayarları,
  presence ve Identity v2. Modlar `LiveScript`'ten türer; aksiyonlar `On(…)`, `[LiveAction]`, `ActionReceived` ile gelir.
  Okuma customer UUID ile, EventFabric'e yazma yalnız runtime token ile. Eski GTAVScriptHook core'u ve modları desteklenmez.
  `Runtime.ini`'ye `Live*` anahtarları eklendi (eksikse varsayılanlar). Ayrıntı: `docs/StreamEmber-Live.md`.

## 1.1
- ChaosModV (C++) incelemesinden gelen yeni API'ler (ayrıntı: `docs/StreamEmber-API.md`):
  `World.SnowOnGround`, `World.SkyDisabled`, `World.FreeColliderSlots`, `Entity.HasCollider`, `Entity.ApplyForceSafe`,
  `Vehicle.IsOutOfControlState`, `Vehicle.IsBrakePressed`, `Vehicle.ScaleMatrix`, `VehicleXenonColorTable`,
  `GTA.UI.Minimap`, `GTA.UI.ScreenDraw` (2D çizgi, WorldToScreen), `WaterQuads`, `Game.AllowRestrictedModelSpawning`,
  `Game.DisableOnlineVehicleDespawn`, `Game.PatchScriptCode`, `Audio.PlayAmbientSpeechAtPosition`.
- Deneysel oyun kancaları (native x64 detour motoru, yalnız ilk kullanımda kurulur, domain kapanınca geri alınır):
  `Game.ScriptThreadsBlocked`, `AudioOverride` (perde, filtreler, ses), `GTA.UI.ScreenShader` (kendi HLSL'in).

## 1.0
- İlk StreamEmber dağıtımı: `StreamEmber.Runtime.GTAV.asi` + `StreamEmber.Scripting.GTAV.dll`, `StreamEmber\` klasör
  düzeni (Runtime, Scripts, Config, Logs, Manifests, Licenses), kendi sürüm numaraları.
- v2 API'si, `.pdb` ve `.xml` dosyaları dağıtımdan çıkarıldı.
- Çökme sertleştirmeleri (bkz. README, "Upstream'den farklarımız").
- GitHub Actions: derleme, testler, `main`'e her push'ta otomatik release.
