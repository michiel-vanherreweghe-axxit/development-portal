import type { Run, WatchRuns } from "../api/types";
import { Spinner } from "../ui/Spinner";
import { ago, label, latestRun, watchName } from "./model";

/** The run history, oldest first, padded with blanks so every card lines up. */
export function Trace({ runs, limit }: { runs: Run[]; limit: number }) {
  const spoken = runs.map((run) => label(run.conclusion)).join(", ");

  return (
    <div className="trace" role="img" aria-label={`Last ${runs.length} runs, oldest first: ${spoken}`}>
      {Array.from({ length: Math.max(0, limit - runs.length) }, (_, index) => (
        <span className="bar empty" key={`empty-${index}`} />
      ))}
      {[...runs].reverse().map((run) => (
        <a
          className={`bar ${run.conclusion}`}
          key={run.url}
          href={run.url}
          target="_blank"
          rel="noreferrer"
          title={`#${run.number} · ${run.branch ?? "—"} · ${label(run.conclusion)} · ${new Date(run.at).toLocaleString()}`}
        />
      ))}
    </div>
  );
}

export function RunState({ run }: { run: Run | undefined }) {
  return (
    <div className={`state ${run ? run.conclusion : ""}`}>
      {run ? (
        <>
          <b>{label(run.conclusion)}</b>
          {ago(run.at)}
        </>
      ) : (
        <b>no runs</b>
      )}
    </div>
  );
}

interface WorkflowRowProps {
  watch: WatchRuns;
  limit: number;
  pending: boolean;
  onDrop: (watch: WatchRuns) => void;
}

export function WorkflowRow({ watch, limit, pending, onDrop }: WorkflowRowProps) {
  const latest = latestRun(watch);
  const name = watchName(watch);

  return (
    <div className={`row${pending ? " pending" : ""}`}>
      <div className="wf">
        <b>{name}</b>
        <small>
          {watch.workflowFile}
          {latest ? ` · #${latest.number} · ${latest.branch ?? "—"}` : ""}
        </small>
      </div>
      {watch.error ? (
        <div className="err">{watch.error}</div>
      ) : (
        <>
          <Trace runs={watch.runs} limit={limit} />
          <RunState run={latest} />
        </>
      )}
      {pending ? (
        <Spinner label={`Removing ${name}`} />
      ) : (
        <button
          className="drop"
          title={`Stop watching ${name}`}
          aria-label={`Stop watching ${watch.workflowFile} in ${watch.repository}`}
          onClick={() => onDrop(watch)}
        >
          ✕
        </button>
      )}
    </div>
  );
}
