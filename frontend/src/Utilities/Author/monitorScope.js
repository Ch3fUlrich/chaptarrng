// Monitoring scopes offered by the author "monitor" dialog. "all" covers BOTH media; the two
// narrower keys are "all" restricted to one medium. Everything else keeps the page's selected medium.
export const ALL_EBOOKS = 'allEbooks';
export const ALL_AUDIOBOOKS = 'allAudiobooks';

export function resolveMonitorScope(selection, fallbackMediaType) {
  switch (selection) {
    case 'all':
      return { monitor: 'all', mediaType: null };
    case ALL_EBOOKS:
      return { monitor: 'all', mediaType: 'ebook' };
    case ALL_AUDIOBOOKS:
      return { monitor: 'all', mediaType: 'audiobook' };
    default:
      return { monitor: selection, mediaType: fallbackMediaType };
  }
}
