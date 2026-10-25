// "EBOOK DEU" / "AUDIO ENG" label for one version row of a work; pure so it can be tested with node --test.
export function versionLabel(version) {
  const medium = version.mediaType === 'audiobook' ? 'AUDIO' : 'EBOOK';
  const language = (version.language || '').toUpperCase();
  return language ? `${medium} ${language}` : medium;
}

export function versionState(version) {
  if (version.hasFiles) {
    return 'have';
  }

  return version.monitored ? 'wanted' : 'none';
}
