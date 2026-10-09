# Stream Ember Runtime (GTA V) — SHVDN'de olmayan API'ler

Bu sayfa, Stream Ember Runtime'ın SHVDN v3 API'sine eklediği oyun mekaniklerini listeler. Hepsi **ChaosModV 2.2.1**
(C++, GPL-3.0) kaynağı incelenerek bulundu: kaos modunun `Memory/*.h` ve `Memory/Hooks/*` dosyalarındaki desenler,
ofsetler ve oyun fonksiyonları. Kod StreamEmber'in kendi C# yazımıdır; desen/ofset bilgisinin kaynakları:
ChaosModV, Menyoo (kar), CitizenFX (2D çizgi), Rainbomizer (script programı, crSkeleton).

Hepsi **GTA V Legacy** desenleridir (runtime Enhanced'da çalışmaz). Her özellik ilk kullanıldığında ayrı ayrı çözülür:
desen bulunamazsa yalnız o özellik devre dışı kalır, `StreamEmber\Logs\Runtime.log`'a bir kez `[StreamEmber]` uyarısı
yazılır, API sessizce `false` / no-op döner. Kod yamaları orijinal baytları saklar ve **script domain kapanınca
(scriptler yeniden yüklenince ya da oyundan çıkınca) geri alınır**.

## Bellek yamaları ve oyun fonksiyonları (`source/core/StreamEmberMemory.cs`)

| API | Ne yapar | Nasıl |
|---|---|---|
| `World.SnowOnGround` | Hava ne olursa olsun yerde/yolda kar | "Kar yağıyor mu" dallarını `jmp` yapar (3095 öncesi/sonrası iki desen) |
| `World.SkyDisabled` | Gökyüzü (kubbe, bulut, güneş, ay) çizilmez | Gökyüzü çizim fonksiyonunun ilk baytı `ret` |
| `World.FreeColliderSlots`, `World.IsPhysicsBudgetAvailable` | phSimulator'da boş collider yeri (>50 = güvenli) | SHVDN'in collider sayaçları |
| `Entity.HasCollider` | Varlığın fiziği aktif mi | Oyunun `CEntity::GetColliderNonConst`'u |
| `Entity.ApplyForceSafe`, `ApplyForceCenterOfMassSafe` | Collider yeri yoksa ve varlık fiziksel değilse kuvveti atlar | Çok sayıda varlığa kuvvet uygulayınca oluşan çökmeyi önler |
| `Entity.GetFragmentGroupBoneIndex(i)` | Fragment grubunun kemiği (`EntityBone.FragmentGroupIndex`'in tersi) | SHVDN fragment tablosu |
| `Vehicle.IsOutOfControlState` | Aracın "kontrolden çıktı" biti (sürücü ölmez, patlamaz) | `CVehicle` bayrak ofseti (desenle bulunur) |
| `Vehicle.IsBrakePressed` | Fren girdisi (oyuncu ya da AI) | `CVehicle` fren girdisi ofseti |
| `Vehicle.ScaleMatrix(m)` | Görsel küçültme/büyütme (her karede çağrılır) | Render ve fizik matrisinin 3x3 kısmı |
| `VehicleXenonColorTable.Override/Restore/RestoreAll` | 13 xenon far renginin global tablosu | Tablo işaretçisi desenle bulunur |
| `GTA.UI.Minimap.SetOffset/SetScale/Reset` | Mini haritayı taşı/büyüt | Minimap düzen verisi (3 katman) + oyunun yenileme fonksiyonu |
| `GTA.UI.ScreenDraw.Line` | Kare başına 2D çizgi (native'lerde yok) | `DRAW_RECT`'in iç listesi; 500 sınırı 5000'e çıkarılır (CitizenFX) |
| `GTA.UI.ScreenDraw.WorldToScreen` | Oyunun kendi projeksiyonu (ekran dışı koordinat da verir) | Oyun fonksiyonu |
| `WaterQuads.GetHeight/SetHeight/SetAllHeights/RestoreAll` | 821 su dörtgeninin yüksekliği (denizi/gölleri kaldırma) | `CWaterQuad` dizisi (0x1C bayt, Z +0x14) |
| `Game.AllowRestrictedModelSpawning` | Online'a özel araç/ped/obje modelleri tek oyunculuda oluşturulabilir | Model kontrolünün 24 baytı NOP |
| `Game.DisableOnlineVehicleDespawn()` | `shop_controller` scripti online araçları silmez | Script bayt kodu yaması (Rainbomizer) |
| `Game.PatchScriptCode(script, pattern, offset, bytes)` | Herhangi bir yüklü oyun scriptinin bayt kodunu yamala | `scrProgramRegistry::FindProgramByHash` + kod sayfalarında desen arama |
| `Audio.PlayAmbientSpeechAtPosition` | Ped olmadan bir noktada ses (konuşma) | `PLAY_AMBIENT_SPEECH_FROM_POSITION_NATIVE` |

## Oyun fonksiyonu kancaları (deneysel, `source/core/StreamEmberHooks.cs` + `StreamEmberNativeCode.cs`)

| API | Kanca | Ne yapar |
|---|---|---|
| `Game.ScriptThreadsBlocked`, `Game.ScriptThreadBlockSupported` | `rage::scrThread::Run` | Oyunun kendi scriptlerini (görev, ambiyans, dükkân) duraklatır; `main`, `main_persistent`, `control_thread` çalışmaya devam eder (Script Hook V ve .NET scriptleri onların içinde) |
| `AudioOverride.Pitch / LowPassCutoff / HighPassCutoff / Volume`, `SetPitchFromSpeedMultiplier`, `ResetAll` | `rage::audSound::CombineBuffers` | Oyundaki bütün seslerin perdesi (sent, ±5000), alçak/yüksek geçiren filtre, ses düzeyi |
| `GTA.UI.ScreenShader.Override(target, hlsl)`, `Reset`, `IsSupported`, `LastError` | `rage::CreateShader` + Script Hook V present callback | Kendi HLSL'ini (ps_4_0, `main`) d3dcompiler_47 ile derler, oyunun `PS_LensDistortion` (tam ekran) ya da `PS_snow` shader'ının yerine koyar, shader önbelleğini render thread'inde yeniler |

Tasarım:

- Kancalar **yalnız ilk kullanıldığında** kurulur, oyun açılırken hiçbir şey kancalanmaz.
- Kanca gövdeleri **native x64 kodu**dur (C#'tan üretilir): ses ve render thread'inde managed kod çalışmaz (GC beklemesi
  yok, script domain kapandıktan sonra sarkan delegate yok). Scriptler yalnız native bir "kontrol bloğu"ndaki değerleri
  değiştirir. Üretilen kod ve kontrol blokları serbest bırakılmaz (birkaç yüz bayt).
- Detour motoru yalnız tamamen tanıdığı, konumdan bağımsız prolog komutlarını taşır (RIP-relative, dallanma yok).
  Fonksiyon başka bir mod tarafından kancalanmışsa (`jmp`) ya da prolog tanınmıyorsa kanca kurulmaz, özellik
  "desteklenmiyor" der. Yazarken ilk iki bayt önce `jmp $` yapılır (yarım yazılmış koda giren thread bekler).
- Script domain kapanırken bütün kontrol blokları kapatılır ve detour'lar geri alınır (bloklanmış oyun scriptleri,
  değişmiş ses, değişmiş shader kalmaz).
- Test: `StreamEmberNativeCode.cs` Windows'a bağımlı değildir. Linux'ta Windows x64 çağrı düzeninde (ms_abi) yazılmış,
  oyunun prologlarıyla aynı hedef fonksiyonlara karşı 29 test geçti (detour kurma/kaldırma, script bloklama, ses
  değerleri ve sınırları, shader argümanlarının 5'i de, present yenilemesi). Oyunda henüz denenmedi.

## ChaosModV'de olup bilinçli eklenmeyenler

| ChaosModV | Neden yok |
|---|---|
| `GetLabelText` kancası (özel GXT etiketleri) | C# tarafında metin `STRING` + alt bileşenle zaten verilebiliyor |
| `HandleToEntityStruct` kancası (silinen aracın handle'ını yenisine yönlendirme) | Yalnız görev aracını değiştirme efektinde gerekiyor; motorun her handle çözümüne girer, risk/kazanç kötü |
| `crSkeleton::GetGlobalMtx` çökme düzeltmesi | `SET_PED_SHOOTS_AT_COORD` ile ilgili; kaos modu portunda bu çağrıyı kullanan efekt yok |
| `ApplyChangeSetEntry` (DLC dosya filtresi), açılış ekranı/DLC atlama | Kaos modu ile ilgisi yok |
| `IDXGISwapChain::Present` kancası | Script Hook V'in present callback'i yeterli; overlay (ui-runtime) zaten kendi çizimini yapıyor |
