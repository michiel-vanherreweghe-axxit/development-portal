import { describe, expect, it } from "vitest";

import type { PlanItem } from "../api/types";
import { addDays, mondayOf, parseIsoDate, planTargets, shift, summary, toIsoDate, visibleItems, weekDays, weekRange } from "./model";

const item = (overrides: Partial<PlanItem> = {}): PlanItem => ({
  id: 1,
  kind: "Todo",
  title: "Write report",
  description: null,
  repository: null,
  issueNumber: null,
  issueState: null,
  issueUrl: null,
  span: "Day",
  date: "2026-10-21",
  isCompleted: false,
  completedAt: null,
  ...overrides,
});

const day = (iso: string) => parseIsoDate(iso);

describe("mondayOf", () => {
  it("keeps a Monday and moves a Sunday back to the Monday before it", () => {
    // Act
    const monday = mondayOf(day("2026-10-19"));
    const sunday = mondayOf(day("2026-10-25"));

    // Assert
    expect(toIsoDate(monday)).toBe("2026-10-19");
    expect(toIsoDate(sunday)).toBe("2026-10-19");
  });

  it("crosses a year boundary", () => {
    // Act
    const monday = mondayOf(day("2027-01-01"));

    // Assert
    expect(toIsoDate(monday)).toBe("2026-12-28");
  });
});

describe("weekRange", () => {
  it("spans Monday to Sunday", () => {
    // Act
    const range = weekRange(day("2026-10-21"));

    // Assert
    expect(range).toEqual({ from: "2026-10-19", to: "2026-10-25" });
    expect(weekDays(day("2026-10-21"))).toHaveLength(7);
  });
});

describe("toIsoDate", () => {
  it("keeps the local day late in the evening", () => {
    // Arrange
    const lateEvening = new Date(2026, 9, 21, 23, 59, 59);

    // Act
    const iso = toIsoDate(lateEvening);

    // Assert
    expect(iso).toBe("2026-10-21");
  });
});

describe("shift", () => {
  it("steps a day or a week, across the end of daylight saving time", () => {
    // Arrange: Europe ends daylight saving on 2026-10-25, so that day has 25 hours.
    const saturday = day("2026-10-24");

    // Act
    const nextDay = shift(saturday, "Day", 1);
    const dayAfter = shift(nextDay, "Day", 1);
    const nextWeek = shift(saturday, "Week", 1);
    const previousWeek = shift(saturday, "Week", -1);

    // Assert
    expect(toIsoDate(nextDay)).toBe("2026-10-25");
    expect(toIsoDate(dayAfter)).toBe("2026-10-26");
    expect(toIsoDate(nextWeek)).toBe("2026-10-31");
    expect(toIsoDate(previousWeek)).toBe("2026-10-17");
    expect(toIsoDate(addDays(day("2026-12-31"), 1))).toBe("2027-01-01");
  });
});

describe("visibleItems", () => {
  const items = [
    item({ id: 1, date: "2026-10-21" }),
    item({ id: 2, date: "2026-10-22" }),
    item({ id: 3, span: "Week", date: "2026-10-19" }),
    item({ id: 4, date: "2026-10-28" }),
    item({ id: 5, span: "Week", date: "2026-10-26" }),
  ];

  it("shows a day with its week's entries and nothing from other weeks", () => {
    // Act
    const groups = visibleItems(items, "Day", day("2026-10-21"));

    // Assert
    expect(groups.map((group) => [group.key, group.items.map((entry) => entry.id)])).toEqual([
      ["week", [3]],
      ["2026-10-21", [1]],
    ]);
  });

  it("groups a week by day, with the week's own entries first", () => {
    // Act
    const groups = visibleItems(items, "Week", day("2026-10-25"));

    // Assert
    expect(groups).toHaveLength(8);
    expect(groups[0]).toMatchObject({ key: "week", date: null });
    expect(groups[0]?.items.map((entry) => entry.id)).toEqual([3]);
    expect(groups.find((group) => group.date === "2026-10-22")?.items.map((entry) => entry.id)).toEqual([2]);
    expect(groups.flatMap((group) => group.items).map((entry) => entry.id)).not.toContain(4);
  });
});

describe("summary", () => {
  it("counts completed against all", () => {
    // Act
    const counted = summary([item({ isCompleted: true }), item({ id: 2 }), item({ id: 3, isCompleted: true })]);

    // Assert
    expect(counted).toEqual({ done: 2, total: 3 });
  });
});

describe("planTargets", () => {
  it("offers the shown day and the week in day mode", () => {
    // Act
    const targets = planTargets("Day", day("2026-10-21"));

    // Assert
    expect(targets.map((target) => [target.value, target.span, target.date])).toEqual([
      ["2026-10-21", "Day", "2026-10-21"],
      ["week", "Week", "2026-10-19"],
    ]);
  });

  it("offers every day of the week and the week itself in week mode", () => {
    // Act
    const targets = planTargets("Week", day("2026-10-25"));

    // Assert
    expect(targets).toHaveLength(8);
    expect(targets.at(-1)).toMatchObject({ value: "week", date: "2026-10-19" });
  });
});
