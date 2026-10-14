import dayjs from 'Utilities/Date/dayjsSetup';

function formatShortTimeSpan(timeSpan) {
  if (!timeSpan) {
    return '';
  }

  const duration = dayjs.duration(timeSpan);

  const hours = Math.floor(duration.asHours());
  const minutes = Math.floor(duration.asMinutes());
  const seconds = Math.floor(duration.asSeconds());
  const locale = typeof document === 'undefined' ? undefined : document.documentElement.lang;
  const formatUnit = (value, unit) => new Intl.NumberFormat(locale, {
    style: 'unit',
    unit,
    unitDisplay: 'long'
  }).format(value);

  if (hours > 0) {
    return formatUnit(hours, 'hour');
  }

  if (minutes > 0) {
    return formatUnit(minutes, 'minute');
  }

  return formatUnit(seconds, 'second');
}

export default formatShortTimeSpan;
