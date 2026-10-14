# UI localization roadmap

## Catalog coverage

ChaptarrNG stores UI strings in the existing `NzbDrone.Core/Localization/Core/*.json` catalogs. The target locales are Spanish (`es`), Simplified Chinese (`zh_CN`), Hindi (`hi`), German (`de`), Japanese (`ja`), French (`fr`), Brazilian Portuguese (`pt_BR`), Arabic (`ar`), Korean (`ko`), Traditional Chinese (`zh_TW`), Vietnamese (`vi`), and Indonesian (`id`).

Each target catalog contains a non-empty value for every one of the 2,705 keys in `en.json`. Product and protocol names, identifiers, placeholders, sample names, and values whose spelling is shared by the target language remain unchanged where appropriate. `LocalizationService.GetDictionary` keeps the English-source fallback for future keys that have not yet been added to a locale file.

## Validation and review

Validation checks JSON validity, duplicate keys, exact key parity, non-empty values, and placeholder, HTML-tag, and URL-token signatures for all 2,705 keys in each of the 12 target catalogs. A source scan checks long English source clauses for untranslated copies and unexpected writing systems in the target catalogs. The follow-up replaces hard-coded English UI in queue and history states, error and empty-state messages, search and import flows, deletion confirmations, settings and profile dialogs, notifications, help text, book-file counts, readable durations, and keyboard-shortcut descriptions. The bundle-recovery screen fetches Core strings when available and retains English fallback copy if localization cannot be loaded. The translation-key check covers both standard and fallback translation helpers. `yarn check-translations` passed (1,603 source files scanned), and the React raw-JSX-text scan found no untranslated JSX text.

Machine translation was used as a starting point for the catalog pass, followed by context-focused corrections and review. A fluent-speaker review of every phrase has not been completed; native-language review remains useful for idiom and tone.

Arabic strings are available, but full right-to-left layout acceptance remains open. Review physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

## Follow-up

1. Request fluent-speaker review for each target locale, prioritizing longer help text and settings labels.
2. Keep new ChaptarrNG messages in the existing Core catalogs and check key and placeholder parity in CI.
