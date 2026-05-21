ForkPlus external language files
================================

Add a JSON file in this directory to add or override UI translations without changing code.

File name:

```text
<language-code>.json
```

Example:

```json
{
  "code": "ja",
  "name": "日本語",
  "translations": {
    "Preferences": "設定",
    "General": "一般",
    "Language": "言語"
  }
}
```

Notes:

- `code` is the value saved in user settings, for example `ja`, `ko`, `fr`, or `zh-Hans`.
- `name` is shown in the Appearance > Language menu.
- `translations` maps the original English UI string to the translated text.
- For built-in languages (`zh-Hans`, `zh-Hant`), entries in JSON files override built-in translations.
- Missing translations fall back to the original English text.
