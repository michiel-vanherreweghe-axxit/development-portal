import { useState } from "react";

import type { NewPlanItem } from "../api/types";
import type { PlanTarget } from "./model";

export function TargetSelect({
  targets,
  value,
  onChange,
  label,
}: {
  targets: PlanTarget[];
  value: string;
  onChange: (value: string) => void;
  label: string;
}) {
  return (
    <select className="plan-select" aria-label={label} value={value} onChange={(event) => onChange(event.target.value)}>
      {targets.map((target) => (
        <option key={target.value} value={target.value}>
          {target.label}
        </option>
      ))}
    </select>
  );
}

interface TodoFormProps {
  targets: PlanTarget[];
  onAdd: (item: NewPlanItem) => Promise<boolean>;
}

export function TodoForm({ targets, onAdd }: TodoFormProps) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [chosen, setChosen] = useState<string | null>(null);

  // The chosen target may not exist in another view; the first one is the fallback.
  const target = targets.find((candidate) => candidate.value === chosen) ?? targets[0]!;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();

    const added = await onAdd({
      kind: "Todo",
      title: title.trim(),
      description: description.trim() || undefined,
      span: target.span,
      date: target.date,
    });

    if (added) {
      setTitle("");
      setDescription("");
    }
  };

  return (
    <form className="plan-form" onSubmit={(event) => void submit(event)}>
      <div className="plan-line">
        <input
          value={title}
          maxLength={200}
          placeholder="Add a to-do"
          aria-label="To-do title"
          onChange={(event) => setTitle(event.target.value)}
        />
        <TargetSelect targets={targets} value={target.value} onChange={setChosen} label="Plan the to-do for" />
        <button className="btn" type="submit" disabled={!title.trim()}>
          Add
        </button>
      </div>
      <input
        value={description}
        maxLength={2000}
        placeholder="Description (optional)"
        aria-label="To-do description"
        onChange={(event) => setDescription(event.target.value)}
      />
    </form>
  );
}
