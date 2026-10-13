# UI localization roadmap

## Catalog coverage

ChaptarrNG stores UI strings in the existing `NzbDrone.Core/Localization/Core/*.json` catalogs. The target locales are Spanish (`es`), Simplified Chinese (`zh_CN`), Hindi (`hi`), German (`de`), Japanese (`ja`), French (`fr`), Brazilian Portuguese (`pt_BR`), Arabic (`ar`), Korean (`ko`), Traditional Chinese (`zh_TW`), Vietnamese (`vi`), and Indonesian (`id`).

Each target catalog contains a translated, non-empty value for every one of the 2,300 keys in `en.json`. Product and protocol names, identifiers, placeholders, sample names, and values whose spelling is shared by the target language remain unchanged where appropriate. `LocalizationService.GetDictionary` keeps the English-source fallback for future keys that have not yet been added to a locale file.

## Validation and review

The review checked JSON validity, exact key parity, non-empty values, and placeholder, HTML-tag, and URL signatures for the 434 changed locale keys. The Naming Builder changes replace hard-coded English labels and validation messages with catalog keys. `yarn check-translations` confirms that frontend `translate()` keys exist in `en.json`.

Machine translation was used as a starting point for the catalog pass, followed by context-focused corrections and review. A fluent-speaker review of every phrase has not been completed; native-language review remains useful for idiom and tone.

Arabic strings are available, but full right-to-left layout acceptance remains open. Review physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

## Follow-up

1. Request fluent-speaker review for each target locale, prioritizing longer help text and settings labels.
2. Keep new ChaptarrNG messages in the existing Core catalogs and check key and placeholder parity in CI.
