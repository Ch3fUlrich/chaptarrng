# D-4228 Core suite regression repair

The supplied merge run has 38 failures: 33 fail in migration 124 because
`QualityDefinitions.Weight` is not persisted (see `TableMapping` and migration
001); five assert the pre-D-3454 illustrated-format behavior. DI, remote path
mapping, Bookshelf conversion and index tests share that migration failure, not
independent service or query defects.

## Changed tests (none deleted or skipped)

- `conflicting_reserved_quality_rolls_back`: use the actual persisted schema; test conflicting titles for all three reserved IDs and preserve size limits, rather than an impossible persisted weight conflict.
- `preserves_grouped_custom_and_malformed_profiles`: remove the invented Weight column and verify all three inserted titles and size defaults against the real schema.
- `should_reject_known_unsupported_indexer_file_types`: move CBR/CBZ cases to supported-format coverage per D-3454; retain DJVU/MKV rejection for ordinary books.
- `should_accept_supported_indexer_file_types`: include CBZ/CBR/CB7 as supported Ebook containers per D-3454.
- `should_reject_sons_of_ares_cbr_before_download` → `should_accept_sons_of_ares_cbr_before_download`: this illustrated publication is now supported, not a hard format rejection.
- `should_reject_known_unsupported_title_file_extensions_when_indexer_file_type_is_missing`: move the comic CBZ case to the dedicated acceptance test; retain video/HTML rejection.
- `should_accept_comic_title_extension_when_indexer_file_type_is_missing` (new): preserve the exact Free Comic Book Day CBZ regression input with the required acceptance assertion.
- `should_not_treat_known_unsupported_file_type_as_ebook_media_type_hint`: retain rejection with genuinely unsupported DJVU/MKV; move the illustrated CBR input to the positive test.
- `should_treat_illustrated_file_type_as_ebook_media_type_hint` (new): the original Sons of Ares CBR input must now identify Ebook.
- `should_reject_illustrated_file_type_for_ordinary_audiobook_request` (new): all three illustrated containers remain incompatible with an ordinary audiobook request.

No genre-based ebook rejection is added: D-3454 maps illustrated containers onto
Ebook, not a separate media type. Ordinary unsupported formats and cross-media
requests retain their hard rejections.
