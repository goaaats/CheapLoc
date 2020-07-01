# ReflexLoc

ReflexLoc is a .NET localization library that is easy to integrate, very low-effort to maintain and does not require maintaining a separate localization database.

It outputs JSON files that can be used together with popular localization tools like Crowdin.

### Integration Example

To setup the localization library, call ``Loc.Setup()``.

```cs
var allowedLang = new [] { "de", "ja", "fr", "it", "es" };

var currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
Log.Information("Trying to set up Loc for culture {0}", currentUiLang);

if (allowedLang.Any(x => currentUiLang == x))
{
    Loc.Setup(File.ReadAllText($"loc_{currentUiLang}.json"));
}
else
{
    Loc.SetupWithFallbacks();
}
```

To receive a localized string, call ``Loc.Localize()``.

```cs
var locText = Loc.Localize("StringKey", "Hello, World.");
```

To generate localization files, call ``Loc.ExportLocalizable()``.
You can specify the target assembly in each function of ``Loc``.
