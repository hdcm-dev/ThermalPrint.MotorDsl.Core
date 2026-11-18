# Scripts mobile — Publicación de APK Android

Empaquetado de APK firmado (`Release`, `arm64`) para distribución manual o
instalación vía `adb install`. Cada script encadena:

1. `dotnet clean`.
2. `dotnet publish -c Release -p:RuntimeIdentifier=android-arm64 -p:AndroidPackageFormat=apk -p:EmbedAssembliesIntoApk=true`.
3. Localización del `*-Signed.apk` resultante.
4. Copia a `out/mobile/<NombreSample>.apk`.

> `EmbedAssembliesIntoApk=true` es **obligatorio** — sin esto el APK no se
> instala manualmente vía ADB (sólo via `dotnet build -t:Run`).

---

## 📋 Catálogo

| Script | Sample que empaqueta |
|---|---|
| `publish-MotorDsl.SampleApp-apk.bat` | `samples/MotorDsl.SampleApp/` |
| `publish-MotorDsl.MultaApp-apk.bat` | `samples/MotorDsl.MultaApp/` |
| `publish-MotorDsl.Integrated.MultaApp-apk.bat` | `samples/MotorDsl.Integrated.MultaApp/` |
| `publish-MotorDsl.Nuget.MultaApp-apk.bat` | `samples/MotorDsl.Nuget.MultaApp/` |
| `publish-MotorDsl.Nuget.Integrated.MultaApp-apk.bat` | `samples/MotorDsl.Nuget.Integrated.MultaApp/` |
| `publish-all.bat` | Empaqueta los 5 en cadena. |
| `update-packages.bat` | Mismo helper que `scripts/local/update-packages.bat` — actualiza `<PackageReference>` de los samples Nuget. |

---

## 🚀 Uso típico

```bat
cd scripts\mobile
publish-MotorDsl.Nuget.Integrated.MultaApp-apk.bat
```

El script soporta un argumento opcional con la URL del backend, que se aplica
patcheando `Resources/Raw/motordsl-config.json`:

```bat
publish-MotorDsl.MultaApp-apk.bat http://{host-de-pruebas}:5000
```

Cuando termina, queda el APK firmado en
`out/mobile/<NombreSample>.apk`. Para instalar:

```bat
adb install -r out\mobile\MotorDsl.Nuget.Integrated.MultaApp.apk
```

---

## 📦 publish-all.bat

```bat
cd scripts\mobile
publish-all.bat
```

Encadena los 5 scripts. Útil antes de un release para validar que todos
compilan.

> El call a `update-packages.bat` está comentado por default. Habilitarlo si
> se quiere subir los `<PackageReference>` de los samples Nuget a la última
> versión publicada antes de empaquetar.

---

## 🐛 Troubleshooting

- **"library libstdc++.so.6 not found"**: típico si se mezclan versiones de
  QuestPDF con runtimes de Android. Asegurar que el sample no referencie
  QuestPDF (los actuales usan PdfSharpCore).
- **APK demasiado grande (> 80 MB)**: revisar que no se estén embebiendo
  fuentes/imágenes innecesariamente. `MauiAsset Include="Resources\Raw\**"`
  embebe todo el directorio.
- **Dispositivo con páginas de 16 KB (Pixel 9, Galaxy S25)**: el `.csproj` ya
  configura `AndroidLdFlags=-Wl,-z,max-page-size=16384`. Si aparece el error
  *"This APK is not 16 KB compatible"*, validar que la flag siga presente.

---

## 🔗 Relación con otros scripts

- `scripts/local/Readme.md` — para correr en un dispositivo conectado sin
  empaquetar APK.
- `scripts/nuget/notas.md` — para publicar paquetes en nuget.org antes de
  rebuild.
