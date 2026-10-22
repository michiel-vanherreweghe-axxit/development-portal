import type { PlanItem, PlanSpan } from "../api/types";

/** Dates stay plain local days; going through UTC would roll a late evening onto the next day. */
export const toIsoDate = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

export const parseIsoDate = (iso: string) => {
  const [year = 0, month = 1, day = 1] = iso.split("-").map(Number);

  return new Date(year, month - 1, day);
};

/** Day arithmetic on the calendar date, so a daylight-saving change never moves the result off midnight. */
export const addDays = (date: Date, days: number) =>
  new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);

/** Weeks start on Monday, like the api's weekly entries. */
export const mondayOf = (date: Date) => addDays(date, -((date.getDay() + 6) % 7));

export const weekDays = (date: Date) => {
  const monday = mondayOf(date);

  return Array.from({ length: 7 }, (_, offset) => addDays(monday, offset));
};

/** What to ask the api for: the whole week either way, since a day view also shows the week's entries. */
export const weekRange = (date: Date) => {
  const days = weekDays(date);

  return { from: toIsoDate(days[0]!), to: toIsoDate(days[6]!) };
};

export const shift = (date: Date, span: PlanSpan, direction: 1 | -1) =>
  addDays(date, (span === "Week" ? 7 : 1) * direction);

export interface PlanGroup {
  key: string;
  /** Null for the entries that belong to the week as a whole. */
  date: string | null;
  items: PlanItem[];
}

/**
 * Day mode: the week's own entries, then that day's. Week mode: the week's own entries, then every
 * day of the week. Entries of other weeks are never shown.
 */
export function visibleItems(items: PlanItem[], span: PlanSpan, date: Date): PlanGroup[] {
  const days = weekDays(date).map(toIsoDate);
  const monday = days[0]!;

  const shownDays = span === "Week" ? days : [toIsoDate(date)];

  return [
    {
      key: "week",
      date: null,
      items: items.filter((item) => item.span === "Week" && item.date === monday),
    },
    ...shownDays.map((day) => ({
      key: day,
      date: day,
      items: items.filter((item) => item.span === "Day" && item.date === day),
    })),
  ];
}

export const summary = (items: PlanItem[]) => ({
  done: items.filter((item) => item.isCompleted).length,
  total: items.length,
});

export interface PlanTarget {
  value: string;
  label: string;
  span: PlanSpan;
  date: string;
}

const dayLabel = (date: Date) =>
  date.toLocaleDateString(undefined, { weekday: "short", day: "numeric", month: "short" });

/** Where a new entry can go from the current view: the week itself, or one of the days shown. */
export function planTargets(span: PlanSpan, date: Date): PlanTarget[] {
  const monday = toIsoDate(mondayOf(date));
  const days = span === "Week" ? weekDays(date) : [date];

  return [
    ...days.map((day) => ({ value: toIsoDate(day), label: dayLabel(day), span: "Day" as const, date: toIsoDate(day) })),
    { value: "week", label: "This week", span: "Week" as const, date: monday },
  ];
}
