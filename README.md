# Stream Ember Runtime (GTA V)

Grand Theft Auto V (Legacy) için StreamEmber'in .NET script çalışma ortamı. Oyunun içinde .NET Framework 4.8'i başlatır,
`StreamEmber\Scripts\` klasöründeki scriptleri yükler ve onlara `StreamEmber.Scripting.GTAV` API'sini verir.
[Community Script Hook V .NET](https://github.com/scripthookvdotnet/scripthookvdotnet) (SHVDN, zlib) üzerine kuruludur;
kendi sürüm numarası, adları ve klasör düzeni olan bağımsız bir dağıtımdır.

```text
GTA5.exe
 └─ ScriptHookV.dll              Alexander Blade (dev-c.com) — native çağrılar, script fiber'ları (ayrıca kurulur)
     └─ StreamEmber.Runtime.GTAV.asi            bu repo: .NET çalışma ortamı
         └─ StreamEmber.Scripting.GTAV.dll      bu repo: scriptlerin API'si (namespace GTA)
             └─ StreamEmber\Scripts\*.dll       scriptler (ör. StreamEmber Trainer: gtav-trainer-scripthook, StreamEmber modları)
```

## Oyun klasöründeki düzen

| Dosya | Görev |
|---|---|
| `StreamEmber.Runtime.GTAV.asi` | Çalışma ortamı. ASI yükleyici (`dinput8.dll`) oyun kökünden yükler |
| `StreamEmber\Runtime\StreamEmber.Scripting.GTAV.dll` | Script API'si |
| `StreamEmber\Config\Runtime.ini` | Ayarlar (konsol tuşu F4, script zaman aşımı, scripts klasörü). Güncellemede korunur |
| `StreamEmber\Scripts\` | Scriptler |
| `StreamEmber\Logs\Runtime.log` | Log |
| `StreamEmber\Manifests\StreamEmber.Runtime.GTAV.json` | Paket manifest'i: sürüm, commit, dosyalar ve SHA-256 değerleri |
| `StreamEmber\Licenses\StreamEmber.Runtime.GTAV\` | Lisanslar |

Gereken: `ScriptHookV.dll` ve `dinput8.dll` ([dev-c.com](http://www.dev-c.com/gtav/scripthookv/)), GTA V **Legacy** (`GTA5.exe`).
Enhanced (`GTA5_Enhanced.exe`) desteklenmez; çalışma ortamı orada pasif kalır ve sebebini loga yazar.
Resmi SHVDN ile birlikte kullanılmaz: kurulum `ScriptHookVDotNet.asi`'yi `.disabled` yapar.

> Topluluğun SHVDN scriptleri (`ScriptHookVDotNet3.dll`'e göre derlenmiş) bu çalışma ortamında yüklenmez. Scriptler
> `StreamEmber.Scripting.GTAV.dll`'e göre derlenir; API, SHVDN v3 API'siyle aynıdır (`using GTA;`).

## Canlı yayın modları (StreamEmber Live)

Script API'si canlı yayın katmanını içerir: EventFabric/GCore bağlantısı, Falcon oyun ayarları ve Identity v2 kimliği.
Canlı yayın modu `StreamEmber.Live.LiveScript`'ten türer ve aksiyonları Tick gibi olaylarla alır (`On("enemy.spawn", …)`,
`[LiveAction]`, `ActionReceived`, `SettingsChanged`); HTTP yazmaz, ayrı bir core DLL'i taşımaz. Okuma customer UUID ile,
EventFabric'e yazma (presence, GCore reset) yalnız Launcher'ın verdiği runtime token ile yapılır. Ayarlar `Runtime.ini` →
`Live*` anahtarları. Ayrıntı: [docs/StreamEmber-Live.md](docs/StreamEmber-Live.md).

## Sürümler ve yayın

- Sürüm: `VERSION` dosyası `major.minor`, patch = o dosyanın son değiştiği commit'ten bu yana commit sayısı.
  `main`'e her push yeni bir sürümdür: `v1.0.0`, `v1.0.1`, … Minör/majör artırmak için `VERSION`'ı değiştirip pushla.
- GitHub Actions (`.github/workflows/build.yml`): her push ve PR'da derleme + testler; `main`'de ayrıca etiket ve
  GitHub Release (`StreamEmber.Runtime.GTAV-<sürüm>.zip` + `.sha256`). Zip'in kökü = oyun klasörü.
- Yerel derlemeler `-dev` ekiyle damgalanır (`1.0.5-dev`); yayınlanan dosyalarla karışmaz.
- DLL'lerde: dosya ve ürün sürümü = StreamEmber sürümü; API derlemesinin `AssemblyVersion`'ı API seviyesidir (`3.7.0.0`).

## Derleme

Visual Studio 2022+ ("Desktop development with C++" + C++/CLI desteği), .NET Framework 4.8 targeting pack.

```powershell
.\build.ps1                                        # derle + dist\GTAV\ + artifacts\*.zip
.\build.ps1 -Deploy -GamePath "D:\EpicGames\GTAV"  # + oyuna kur (ya da GTAV_GAME_PATH)
.\build.ps1 -Deploy -ResetConfig                   # Runtime.ini'yi de şablonla değiştir
```

## Upstream ile ilişki

| | |
|---|---|
| Upstream | https://github.com/scripthookvdotnet/scripthookvdotnet (`main`) |
| Taban | `4cd31528` (v3.7.0-nightly.192 karşılığı) |
| Remote | `upstream` → resmi repo, `origin` → StreamEmberPlatform/gtav-runtime-scripthook |

Güncelleme: `git fetch upstream && git merge upstream/main`, ardından derleme + oyun testi. Bizim değişiklikler küçük ve
işaretli tutulur (`StreamEmber:` yorumları); isim ve yollar yalnız `source/core/StreamEmberLayout.cs` içindedir.
Upstream'in v2 API'si (`source/scripting_v2`) depoda durur ama derlenmez ve dağıtılmaz.

### Upstream'den farklarımız

| Değişiklik | Neden |
|---|---|
| Adlar ve klasör düzeni (`StreamEmberLayout.cs`), v2 API'si yok, `.pdb`/`.xml` yok | StreamEmber dağıtımı |
| `NativeMemory.cs` `s_isDecoratorLocked`: `Rel32(address, 2, 5)` | Upstream disp32'yi +3'ten okuyor → `Decorator.IsLocked` yanlış bayt |
| Tüm `delegate* unmanaged` çağrılarına null kontrolü | Bulunamayan desen 0 adresini çağırıp oyunu çökertiyordu; artık yalnız ilgili script durur |
| Statik kurucuda desen aramaları ayrı ayrı korumalı | Tek kaçan desen tüm NativeMemory'yi devre dışı bırakıyordu |
| Prop kod yaması yalnız doğrulanmış oyun sürümlerinde (`NewestVerifiedGameVersionId`) | Bilinmeyen build'de yanlış kodu NOP'lama riski |
| `SignalAndWaitWithHangWarning` | Yield etmeyen script için 5 sn'de bir uyarı; script'i öldürmez |
| `DllMain.cpp` tick sınırında try/catch | Sızan managed exception süreci sonlandırıyordu |
| `GTA5_Enhanced.exe` içinde pasif kalma | Enhanced desteklenmiyor |
| `source/core/StreamEmber*.cs`, `source/scripting_v3/GTA/StreamEmber/` (1.1) | SHVDN'de olmayan oyun mekanikleri ve deneysel kancalar, ChaosModV incelemesinden: [docs/StreamEmber-API.md](docs/StreamEmber-API.md) |
| `source/scripting_v3/StreamEmber.Live/`, `StreamEmber.Live.Game/` (1.2) | Canlı yayın katmanı (EventFabric, Falcon ayarları, Identity v2); iki runtime'da aynı kod: [docs/StreamEmber-Live.md](docs/StreamEmber-Live.md) |

Oyun güncellenince: yeni `GameVersion` değerini getiren upstream'i birleştir, `NewestVerifiedGameVersionId`'yi güncelle.

## Lisans

zlib ([LICENSE.txt](LICENSE.txt), [COPYRIGHT.md](COPYRIGHT.md), [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).
Upstream belgeleri: [docs/upstream](docs/upstream/README.md).
