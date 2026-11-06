import { translate } from '../i18n/t.pipe';

const money = new Intl.NumberFormat('da-DK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const wholeMoney = new Intl.NumberFormat('da-DK', { maximumFractionDigits: 0 });
const weekday = new Intl.DateTimeFormat('da-DK', { weekday: 'short' });
const dayMonth = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'numeric' });

/** "9,95", or "18" for whole kroner, like the shelf labels. */
export function formatPrice(amount: number): string {
  return Number.isInteger(amount) ? wholeMoney.format(amount) : money.format(amount);
}

export function formatRounded(amount: number): string {
  return wholeMoney.format(Math.round(amount));
}

function parseDay(day: string): Date {
  const [y, m, d] = day.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function daysFromToday(day: string): number {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return Math.round((parseDay(day).getTime() - today.getTime()) / 86_400_000);
}

/** "kun i dag", "til i morgen", "til fre." within a week, otherwise "til 31.12.". */
export function validUntil(lastValidDay: string): string {
  const days = daysFromToday(lastValidDay);
  if (days <= 0) return translate('tag.onlyToday');
  if (days === 1) return translate('tag.untilTomorrow');
  const label = days < 7 ? weekday.format(parseDay(lastValidDay)) : dayMonth.format(parseDay(lastValidDay));
  return translate('tag.until', { day: label });
}

export function startsOn(day: string): string {
  return translate('offers.startsOn', { day: `${weekday.format(parseDay(day))} ${dayMonth.format(parseDay(day))}` });
}

export function endsSoon(lastValidDay: string): boolean {
  return daysFromToday(lastValidDay) <= 1;
}

const tripDay = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'short' });

/** "søndag 8. nov." for a trip's date. */
export function formatTripDate(iso: string): string {
  const text = tripDay.format(new Date(iso));
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Dark or white text on a coloured band (category or store), whichever reads better on that colour. */
export function inkOn(hex: string): string {
  const channel = (i: number) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  const luminance = 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
  // Contrast against white vs. against #1b2430 (luminance ~0.017).
  return 1.05 / (luminance + 0.05) >= (luminance + 0.05) / 0.067 ? '#ffffff' : '#1b2430';
}
