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
