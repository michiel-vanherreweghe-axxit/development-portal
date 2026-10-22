import { useCallback, useMemo, useState } from "react";

import type { PlanSpan, Repository } from "../api/types";
import { Confirm, type Question } from "../ui/Confirm";
import type { Notify } from "../ui/useToasts";
import { IssueFinder } from "./IssueFinder";
import { parseIsoDate, planTargets, shift, summary, toIsoDate, visibleItems, weekRange } from "./model";
import { PlanItemRow } from "./PlanItemRow";
import { TodoForm } from "./TodoForm";
import { usePlanning } from "./usePlanning";

const long = (date: Date) =>
  date.toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "long", year: "numeric" });

const short = (date: Date) => date.toLocaleDateString(undefined, { day: "numeric", month: "short" });

interface PlanningPageProps {
  repositories: Repository[];
  notify: Notify;
  onUnauthorized: () => void;
}

export function PlanningPage({ repositories, notify, onUnauthorized }: PlanningPageProps) {
  const [span, setSpan] = useState<PlanSpan>("Day");
  const [date, setDate] = useState(() => new Date());
  const [ask, setAsk] = useState<Question | null>(null);

  const { from, to } = useMemo(() => weekRange(date), [date]);
  const planning = usePlanning(notify, onUnauthorized, true, from, to);
  const closeAsk = useCallback(() => setAsk(null), []);

  const groups = useMemo(() => visibleItems(planning.items, span, date), [planning.items, span, date]);
  const targets = useMemo(() => planTargets(span, date), [span, date]);
  const shown = groups.flatMap((group) => group.items);
  const { done, total } = summary(shown);

  const heading = span === "Day" ? long(date) : `Week of ${short(parseIsoDate(from))} – ${short(parseIsoDate(to))}`;

  const remove = (item: (typeof shown)[number]) =>
    setAsk({
      title: "Remove this from your plan?",
      detail: (
        <>
          <code>{item.title}</code> leaves your plan.{" "}
          {item.kind === "Issue" ? "The GitHub issue itself is not touched." : "A to-do cannot be brought back."}
        </>
      ),
      verb: "Remove it",
      cancel: "Keep it",
      onYes: () => void planning.remove(item),
    });

  const dayName = (iso: string | null) =>
    iso === null ? `Planned for the week · ${short(parseIsoDate(from))} – ${short(parseIsoDate(to))}` : long(parseIsoDate(iso));

  return (
    <>
      <div className="toolbar plan-toolbar">
        <button className="fchip" aria-pressed={span === "Day"} onClick={() => setSpan("Day")}>
          Day
        </button>
        <button className="fchip" aria-pressed={span === "Week"} onClick={() => setSpan("Week")}>
          Week
        </button>
        <button className="ghost" aria-label={`Previous ${span.toLowerCase()}`} onClick={() => setDate(shift(date, span, -1))}>
          ←
        </button>
        <button className="ghost" onClick={() => setDate(new Date())}>
          Today
        </button>
        <button className="ghost" aria-label={`Next ${span.toLowerCase()}`} onClick={() => setDate(shift(date, span, 1))}>
          →
        </button>
        <input
          type="date"
          className="plan-jump"
          aria-label="Jump to a date"
          value={toIsoDate(date)}
          onChange={(event) => event.target.value && setDate(parseIsoDate(event.target.value))}
        />
      </div>

      <h2 className="plan-heading">{heading}</h2>
      <p className="plan-sum">
        {total === 0 ? "Nothing planned yet." : `${done} of ${total} done`}
      </p>

      <TodoForm targets={targets} onAdd={(item) => planning.create(item, `Added “${item.title}” to your plan.`)} />
      <IssueFinder
        repositories={repositories}
        targets={targets}
        onFailure={planning.report}
        onAdd={(item, hit) => planning.create(item, `Added ${item.repository}#${hit.number} to your plan.`)}
      />

      {planning.loading && (
        <p className="empty-state" role="status">
          Loading your plan…
        </p>
      )}

      {!planning.loading &&
        groups
          .filter((group) => group.items.length > 0 || group.date !== null)
          .map((group) => (
            <section className="group plan-group" key={group.key}>
              <header>
                <span className="repo">{dayName(group.date)}</span>
                <span className="count">{group.items.length}</span>
              </header>
              {group.items.length === 0 && <p className="none">Nothing planned.</p>}
              {group.items.map((item) => (
                <PlanItemRow
                  key={item.id}
                  item={item}
                  pending={planning.pending === item.id}
                  onChange={planning.change}
                  onRemove={remove}
                />
              ))}
            </section>
          ))}

      <Confirm ask={ask} onClose={closeAsk} />
    </>
  );
}
