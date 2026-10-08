# GTAVScriptHookRuntime

StreamEmber için kendi derlediğimiz **ScriptHookVDotNet (SHVDN)** runtime'ı: `ScriptHookVDotNet.asi` +
`ScriptHookVDotNet2.dll` + `ScriptHookVDotNet3.dll`. Modlarımız (`../GTAVScriptHook`) bunun üzerinde çalışır.
Bu klasör upstream'in bir fork'udur; kendi kodumuz değil. Upstream dosyalarını (README.md, source/…) gerekmedikçe değiştirmeyin.

## Kaynak eşlemesi

| | |
|---|---|
| Upstream | https://github.com/scripthookvdotnet/scripthookvdotnet (`main`) |
| Taban commit | `4cd31528f81b12fb800db8780bbac86074f276e7` (PR #1815, 2026-10-07 15:33 UTC) |
| Karşılık gelen resmi build | `v3.7.0-nightly.192` → assembly sürümü `3.7.0.192` |
| Yerel tag | `v3.7.0-nightly.192` (taban commit'i işaretler) |

Upstream'de kaynak kod yalnız ana repoda tutulur; `main`'e her push'ta CI derleyip
`scripthookvdotnet/scripthookvdotnet-nightly` reposuna release olarak koyar (o repoda kod yoktur, yalnız README + release dosyaları).
Bu yüzden "nightly.N kaynağı" = o build'i tetikleyen `main` commit'idir.

## Git düzeni

- Remote `upstream` → resmi repo. `origin` yok (kendi uzak repomuz açılınca eklenir).
- Çalışma dalı: `streamember/main`. Bizim değişikliklerimiz yalnız burada.
- Güncelleme: `git fetch upstream` → `git merge upstream/main` (ya da istenen nightly'nin commit'i) → yeni nightly no ile tag.
  Hangi commit'in hangi nightly olduğu: https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases (release notunda commit SHA'sı var).

## Klasörler

```text
source/core/            C++/CLI .asi (ScriptHookVDotNet.vcxproj)
source/core/sdk/        SHV SDK alt kümesi (main.h + ScriptHookV.lib) — upstream'de takipli, derleme için yeterli
source/scripting_v2/    API v2 (bakım modu)
source/scripting_v3/    API v3 (asıl API)
vendor/ScriptHookV_SDK_1.0.617.1a/   tam SHV SDK (Downloads'tan kopya) — yalnız başvuru, git dışı (yeniden dağıtımı yasak)
build.ps1               StreamEmber derleme betiği
builds/                 build.ps1 çıktıları (git dışı)
```

`vendor` SDK'sındaki `main.h` ve `ScriptHookV.lib`, `source/core/sdk` içindekilerle birebir aynıdır (2026-10-08'de doğrulandı).

## Derleme

Ön koşullar (Visual Studio 2022+):
- "Desktop development with C++" + **C++/CLI support (v143)** bileşeni
- **.NET Framework 4.8 targeting pack** ve .NET SDK
- Windows 10 SDK (proje `10.0.17763.0` hedefler; yoksa VS'de "Retarget" ya da daha yeni bir Windows SDK)

```powershell
.\build.ps1                      # Release x64, sürüm 3.7.0.192
.\build.ps1 -Version 3.7.0.193   # sürüm damgasını değiştir
.\build.ps1 -Configuration Debug
```

Çıktı: `builds\<sürüm>\` (asi, iki dll, xml dokümanları, ini). Upstream CI'ın komutunun karşılığı:
`msbuild /m /p:configuration=Release /p:platform=x64 /p:SHVDN_VERSION=3.7.0.192 ScriptHookVDotNet.sln`

## Oyuna kurulum

`ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll`, `ScriptHookVDotNet3.dll` **her zaman birlikte** `GTA5.exe` klasörüne kopyalanır.
`ScriptHookVDotNet.ini` nightly formatındadır; eski (3.6.0) ini ile karıştırılmamalı.
Oyunda Alexander Blade'in güncel `ScriptHookV.dll`'i gerekir (dev-c.com).

## GTAVScriptHook ile ilişki

`../GTAVScriptHook/lib/ScriptHookVDotNet3.dll` yalnız derleme başvurusudur. Upstream önerisi: yayınlanan scriptleri
stable API'ye karşı derlemek; nightly'ye özgü API'ler habersiz değişebilir. Nightly-only bir API kullanılacaksa
bu runtime'ın `ScriptHookVDotNet3.dll`'i başvuru olarak alınır ve bilinçli yapılır.
