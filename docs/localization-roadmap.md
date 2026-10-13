# UI localization roadmap

## Catalog coverage

ChaptarrNG stores UI strings in the existing `NzbDrone.Core/Localization/Core/*.json` catalogs and keeps English as the runtime fallback. This change completes key coverage for Spanish (`es`), Simplified Chinese (`zh_CN`), Hindi (`hi`), German (`de`), Japanese (`ja`), French (`fr`), Brazilian Portuguese (`pt_BR`), Arabic (`ar`), Korean (`ko`), Traditional Chinese (`zh_TW`), Vietnamese (`vi`), and Indonesian (`id`). Existing language mappings and UI behavior are unchanged.

Every target file now has one non-empty value for each of the 2,295 keys in `en.json`. Existing locale values are preserved. A key absent from a locale uses its exact English source value, so the catalog has complete key coverage without claiming that every message has been translated. Six obsolete locale-only keys were removed.

## Catalog status

The following counts describe this change against the catalogs on `main` before it was applied:

| Locale | Existing values kept | English fallback values added |
| --- | ---: | ---: |
| Arabic (`ar`) | 676 | 1,619 |
| German (`de`) | 1,133 | 1,162 |
| Spanish (`es`) | 1,135 | 1,160 |
| French (`fr`) | 1,133 | 1,162 |
| Hindi (`hi`) | 673 | 1,622 |
| Indonesian (`id`) | 127 | 2,168 |
| Japanese (`ja`) | 674 | 1,621 |
| Korean (`ko`) | 803 | 1,492 |
| Brazilian Portuguese (`pt_BR`) | 1,135 | 1,160 |
| Vietnamese (`vi`) | 681 | 1,614 |
| Simplified Chinese (`zh_CN`) | 1,133 | 1,162 |
| Traditional Chinese (`zh_TW`) | 193 | 2,102 |

These counts distinguish existing localized strings from English fallbacks. The large fallback share means native-speaker translation and review remain future work. Product names, media metadata, filenames, paths, commands, and protocol/API identifiers should remain unchanged during translation.

## Validation and review

Catalog checks should confirm valid JSON, exact key parity with `en.json`, non-empty values, and preservation of placeholders, rich-text tags, URLs, inline code, and search operators. Exact English fallbacks inherit those source structures. Native-speaker phrase review has not been completed.

Arabic strings are present, but full right-to-left layout acceptance remains open. Review physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

## Follow-up

1. Request fluent-speaker review for each target locale, prioritizing the large fallback catalogs.
2. Translate English fallback values only after preserving placeholders and technical identifiers.
3. Keep new ChaptarrNG messages in the existing Core catalogs and check key and placeholder parity in CI.
