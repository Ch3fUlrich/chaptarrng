# UI localization roadmap

## Existing support and verified gap

ChaptarrNG already has a `Core/*.json` catalog system, server-side fallback to English, and a System > UI Language setting. There are 45 checked-in core catalogs, including Spanish, German, Arabic, Hindi, Japanese, Korean, Vietnamese, Simplified Chinese, Traditional Chinese, and Indonesian. The runtime language list already maps Chinese to `zh_CN` and Brazilian Portuguese to `pt_BR`; this PR adds selectable mappings for the existing Traditional Chinese and Indonesian catalogs.

The AudioBookShelf root-mapping UI adds 17 English messages that were missing from every checked locale. The first slice filled them for the selectable es, de, zh_CN, ja, fr, pt_BR, ar, ko, hi, and vi catalogs. This continuation exposes the existing Indonesian and Traditional Chinese catalogs and translates all 19 root-mapping messages for those two locales. It reuses the current localization service and does not add a second language preference.

## Next implementation steps

1. Keep new Chaptarr-specific messages in the existing core catalog and add them to the highest-priority selectable locale files first.
2. Add a coverage report for recently introduced UI keys and keep token placeholders identical to English.
3. Decide separately whether to expose the existing Spanish-Mexico catalog variant in the UI; do not imply it is selectable until the language mapping supports it.
4. Review forms, tables, navigation, and modals at narrow and desktop widths with the existing Arabic and Hebrew `lang` and `dir` behavior; full RTL layout remains separate.
5. Keep book, author, edition, file, and integration names as supplied; use the UI locale only for interface copy and locale-aware date/number formatting.

## First-pass locale order

This is a practical rollout order, not a claim about these apps' actual user base. Product-level language analytics are not available. It combines global speaker estimates with online-content usage, and keeps the requested Arabic and Asian-language coverage in the first waves:

1. Spanish (es; es-MX where the product already distinguishes it)
2. Simplified Chinese (zh-Hans / zh-CN)
3. Hindi (hi)
4. German (de)
5. Japanese (ja)
6. French (fr)
7. Brazilian Portuguese (pt-BR)
8. Arabic (ar; requires right-to-left layout support)
9. Korean (ko)
10. Traditional Chinese (zh-Hant / zh-TW)
11. Vietnamese (vi)
12. Indonesian (id)

The World Bank's 2025 report estimates Mandarin Chinese, Hindi, and Spanish at 15%, 7.6%, and 6.9% of the global population respectively; its online-content data also places German, Chinese, and Japanese among the leading languages. [World Bank, Digital Progress and Trends Report 2025](https://documents1.worldbank.org/curated/en/099112525160536089/pdf/P505350-59c98ca8-0803-4f23-b470-17f3dab010ab.pdf). W3Techs tracks website content-language share rather than app users. [W3Techs content-language survey](https://w3techs.com/technologies/cross/content_language%20/ranking). Reorder this list if maintainers later receive reliable demand signals.

## Translation boundaries and acceptance

Translate interface labels, help, empty states, validation, notifications, and errors. Keep media titles, user names, filenames and paths, commands, API identifiers, protocol values, and other user-supplied content unchanged. Use the selected UI language for date, time, number, and plural formatting. Every locale must fall back to English for a missing key; CI should check placeholder parity and report missing strings. Arabic ships only after the app sets `lang` and `dir`, uses logical layout properties, and verifies the core workflows in RTL at narrow and desktop widths.

## Implemented in this PR

System > UI Language now offers Indonesian and Traditional Chinese through the existing locale service, alongside the prior first-wave choices. Their AudioBookShelf root-mapping labels, guidance, loading states, and errors are translated, and untranslated messages continue to fall back to English. Arabic and Hebrew continue to use right-to-left document direction; full-page RTL layout review remains separate.

## Added in this continuation

The audio-only and audio-plus-eBooks library choices now use translated labels across all twelve selectable locales. The shared mapping form no longer falls back to English for those two choices; full application catalog completion and page-level RTL review remain separate work.
