function formatDurationMinutes(minutes) {
  if (minutes > 0) {
    const totalMinutes = Math.round(minutes);
    const hours = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    const locale = typeof document === 'undefined' ? undefined : document.documentElement.lang;
    const formatUnit = (value, unit) => new Intl.NumberFormat(locale, {
      style: 'unit',
      unit,
      unitDisplay: 'narrow'
    }).format(value);

    if (hours > 0 && mins > 0) {
      return `${formatUnit(hours, 'hour')} ${formatUnit(mins, 'minute')}`;
    }

    if (hours > 0) {
      return formatUnit(hours, 'hour');
    }

    return formatUnit(mins, 'minute');
  }

  return '';
}

export default formatDurationMinutes;
