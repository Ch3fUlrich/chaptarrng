import dayjs from 'Utilities/Date/dayjsSetup';

function formatReadableDuration(timeSpan) {
  if (timeSpan === null || timeSpan === undefined || timeSpan === '') {
    return '';
  }

  const duration = dayjs.duration(timeSpan);
  const locale = typeof document === 'undefined' ?
    undefined :
    document.documentElement.lang;
  const formatUnit = (value, unit) => {
    return new Intl.NumberFormat(locale, {
      style: 'unit',
      unit,
      unitDisplay: 'short'
    }).format(value);
  };

  const totalHours = Math.floor(duration.asHours());
  const minutes = duration.get('minutes'); // Only the minute portion, not total minutes

  const parts = [];

  if (totalHours > 0) {
    parts.push(formatUnit(totalHours, 'hour'));
  }

  if (minutes > 0) {
    parts.push(formatUnit(minutes, 'minute'));
  }

  // If no hours or minutes, show "0 min"
  if (parts.length === 0) {
    return formatUnit(0, 'minute');
  }

  return parts.join(' ');
}

export default formatReadableDuration;
