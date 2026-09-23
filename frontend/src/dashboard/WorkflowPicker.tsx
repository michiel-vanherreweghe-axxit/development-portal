import { useEffect, useState } from "react";

import { readWorkflows } from "../api/portal";
import type { Watch, Workflow } from "../api/types";
import { Spinner } from "../ui/Spinner";

interface WorkflowPickerProps {
  repository: string;
  watches: Watch[];
  /** The workflow file currently being saved, if any. */
  adding: string | null;
  /** Inside a repository card the picker is bare; on its own it carries a header. */
  bare: boolean;
  onAdd: (workflow: Workflow) => void;
  onClose: () => void;
}

export function WorkflowPicker({ repository, watches, adding, bare, onAdd, onClose }: WorkflowPickerProps) {
  const [workflows, setWorkflows] = useState<Workflow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    readWorkflows(repository)
      .then((found) => !cancelled && setWorkflows(found))
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(failure instanceof Error ? failure.message : "The workflows could not be read.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, [repository]);

  const body = (
    <>
      {!workflows && !error && (
        <p className="empty-state" style={{ border: 0 }}>
          <Spinner label="Loading" /> Loading workflows…
        </p>
      )}
      {error && (
        <p className="empty-state" style={{ border: 0, color: "var(--fail)" }}>
          {error}
        </p>
      )}
      {workflows?.length === 0 && (
        <p className="empty-state" style={{ border: 0 }}>
          This repository has no workflows.
        </p>
      )}
      {workflows?.map((workflow) => {
        const watched = watches.some(
          (watch) => watch.repository === repository && watch.workflowFile === workflow.file,
        );
        return (
          <div className="row" key={workflow.file}>
            <div className="wf">
              <b>{workflow.name}</b>
              <small>{workflow.file}</small>
            </div>
            <button
              className="btn"
              disabled={watched || adding === workflow.file}
              onClick={() => onAdd(workflow)}
            >
              {adding === workflow.file ? (
                <>
                  <Spinner label="Adding" /> Adding…
                </>
              ) : watched ? (
                "Watching"
              ) : (
                "Watch"
              )}
            </button>
          </div>
        );
      })}
    </>
  );

  if (bare) {
    return (
      <div className="addbox">
        <div className="mini">
          Add a workflow
          <button className="drop" style={{ marginLeft: "auto" }} onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        {body}
      </div>
    );
  }

  const [owner, name] = repository.split("/");

  return (
    <div className="group picker">
      <header>
        <a className="repo" href={`https://github.com/${repository}`} target="_blank" rel="noreferrer">
          <span className="owner">{owner}/</span>
          {name}
        </a>
        <button
          className="drop"
          style={{ marginLeft: "auto" }}
          onClick={onClose}
          aria-label="Close workflow picker"
        >
          ✕
        </button>
      </header>
      {body}
    </div>
  );
}
