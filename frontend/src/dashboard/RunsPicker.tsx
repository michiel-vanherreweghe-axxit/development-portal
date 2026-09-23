import { useState } from "react";

import { useDismiss } from "../shell/hooks";
import { IconChevron } from "../shell/icons";

const RUN_STEPS = [5, 10, 15, 20, 30, 50];

export function RunsPicker({ value, onPick }: { value: number; onPick: (limit: number) => void }) {
  const [open, setOpen] = useState(false);
  const box = useDismiss<HTMLDivElement>(open, setOpen);

  return (
    <div className="runsel" ref={box}>
      <button type="button" className="trigger" aria-haspopup="listbox" aria-expanded={open} onClick={() => setOpen((v) => !v)}>
        <span className="lbl">History</span>
        <span className="val">last {value} runs</span>
        <IconChevron />
      </button>
      {open && (
        <div className="opts" role="listbox" aria-label="Runs to show">
          {RUN_STEPS.map((steps) => (
            <button
              key={steps}
              role="option"
              aria-selected={steps === value}
              type="button"
              onClick={() => {
                setOpen(false);
                if (steps !== value) {
                  onPick(steps);
                }
              }}
            >
              last {steps} runs
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
