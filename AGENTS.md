# StreamEmber Runtime (GTA V) — ajan notları

- Önce `README.md`. Bu repo SHVDN'in fork'udur; upstream dosyalarını gerekmedikçe değiştirme, değiştirdiğin yeri
  `StreamEmber:` yorumuyla işaretle (upstream birleştirmeleri kolay kalsın).
- Ürün adları ve oyun klasörü yolları yalnız `source/core/StreamEmberLayout.cs` içinde. Başka yerde
  "ScriptHookVDotNet*.dll", "scripts" gibi sabit yazma. TargetName (vcxproj) ve AssemblyName (API csproj) bu sınıfla aynı olmalı.
- Sürüm `VERSION` + git geçmişinden gelir (`tools/StreamEmber.Build.psm1`); elle sürüm yazma. API `AssemblyVersion`'ı
  API seviyesidir (major 3), değiştirme.
- Dağıtımda `.pdb` / `.xml` olmaz; CI bunu denetler.
- `tools/StreamEmber.Build.psm1` üç repoda (gtav-runtime-scripthook, rdr2-runtime-scripthook, ui-runtime) aynı tutulur.
- Kullanıcıya görünen metinler Türkçe; kod, tanımlayıcılar ve kod yorumları İngilizce.
