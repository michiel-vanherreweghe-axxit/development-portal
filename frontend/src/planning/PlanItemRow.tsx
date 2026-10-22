import { useState } from "react";

import type { IssueState, PlanItem, PlanItemChange, PlanSpan } from "../api/types";
import { Spinner } from "../ui/Spinner";

const STATE: Record<IssueState, { tone: "pass" | "fail" | "idle"; text: string }> = {
  Open: { tone: "pass", text: "Open" },
  Closed: { tone: "idle", text: "Closed" },
  Unavailable: { tone: "fail", text: "Unavailable on GitHub" },
  Unknown: { tone: "idle", text: "State unknown" },
};

/** An entry's whole editable state, with the given parts replaced: the api takes a full update. */
const changeOf = (item: PlanItem, overrides: Partial<PlanItemChange> = {}): PlanItemChange => ({
  ...(item.kind === "Todo" ? { title: item.title, description: item.description } : {}),
  span: item.span,
  date: item.date,
  isCompleted: item.isCompleted,
  ...overrides,
});

interface PlanItemRowProps {
  item: PlanItem;
  pending: boolean;
  onChange: (item: PlanItem, change: PlanItemChange, message: string) => Promise<boolean>;
  onRemove: (item: PlanItem) => void;
}

export function PlanItemRow({ item, pending, onChange, onRemove }: PlanItemRowProps) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(item.title);
  const [description, setDescription] = useState(item.description ?? "");
  const [span, setSpan] = useState<PlanSpan>(item.span);
  const [date, setDate] = useState(item.date);

  const isIssue = item.kind === "Issue";
  const state = item.issueState ? STATE[item.issueState] : null;

  const open = () => {
    setTitle(item.title);
    setDescription(item.description ?? "");
    setSpan(item.span);
    setDate(item.date);
    setEditing(true);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();

    const saved = await onChange(
      item,
      changeOf(item, { ...(isIssue ? {} : { title: title.trim(), description: description.trim() || null }), span, date }),
      `Saved “${isIssue ? item.title : title.trim()}”.`,
    );

    if (saved) {
      setEditing(false);
    }
  };

  return (
    <div className={`plan-item${item.isCompleted ? " done" : ""}${pending ? " pending" : ""}`}>
      <input
        type="checkbox"
        checked={item.isCompleted}
        disabled={pending}
        aria-label={`Mark “${item.title}” as ${item.isCompleted ? "not done" : "done"}`}
        onChange={() =>
          void onChange(
            item,
            changeOf(item, { isCompleted: !item.isCompleted }),
            item.isCompleted ? `Reopened “${item.title}”.` : `Completed “${item.title}”.`,
          )
        }
      />

      <div className="wf">
        {isIssue && item.issueUrl ? (
          <a className="plan-title" href={item.issueUrl} target="_blank" rel="noreferrer">
            {item.title}
          </a>
        ) : (
          <b className="plan-title">{item.title}</b>
        )}
        <small>
          {isIssue ? `${item.repository}#${item.issueNumber}` : (item.description ?? "To-do")}
        </small>
      </div>

      <span className="pill">{isIssue ? "Issue" : "To-do"}</span>
      {state && <span className={`badge ${state.tone}`}>{state.text}</span>}

      {pending ? (
        <Spinner label={`Saving ${item.title}`} />
      ) : (
        <span className="plan-acts">
          <button className="ghost" onClick={editing ? () => setEditing(false) : open}>
            {editing ? "Cancel" : isIssue ? "Move" : "Edit"}
          </button>
          <button className="drop" aria-label={`Remove “${item.title}” from the plan`} onClick={() => onRemove(item)}>
            ✕
          </button>
        </span>
      )}

      {editing && (
        <form className="plan-edit" onSubmit={(event) => void save(event)}>
          {!isIssue && (
            <>
              <input
                value={title}
                maxLength={200}
                aria-label="Title"
                onChange={(event) => setTitle(event.target.value)}
              />
              <input
                value={description}
                maxLength={2000}
                placeholder="Description (optional)"
                aria-label="Description"
                onChange={(event) => setDescription(event.target.value)}
              />
            </>
          )}
          <div className="plan-line">
            <select
              className="plan-select"
              aria-label="Planned for"
              value={span}
              onChange={(event) => setSpan(event.target.value as PlanSpan)}
            >
              <option value="Day">A day</option>
              <option value="Week">The week of</option>
            </select>
            <input type="date" aria-label="Date" value={date} required onChange={(event) => setDate(event.target.value)} />
            <button className="btn" type="submit" disabled={!date || (!isIssue && !title.trim())}>
              Save
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
