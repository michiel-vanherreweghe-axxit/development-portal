import { useState } from "react";

import { useDismiss } from "../shell/hooks";
import { IconChevron, IconPullRequest } from "../shell/icons";
import { Spinner } from "../ui/Spinner";
import { ago, label, review, type PullRequestView } from "./model";

function ChecksBadge({ pullRequest }: { pullRequest: PullRequestView }) {
  const tip = pullRequest.checks.map((check) => `${check.name}: ${label(check.conclusion)}`).join("\n");

  switch (pullRequest.state) {
    case "none":
      return <span className="badge idle">no checks</span>;
    case "fail":
      return (
        <span className="badge fail" title={tip}>
          ✕ {pullRequest.failing} failing
        </span>
      );
    case "run":
      return (
        <span className="badge run" title={tip}>
          <i className="pip" />
          {pullRequest.running} running
        </span>
      );
    default:
      return (
        <span className="badge pass" title={tip}>
          ✓ {pullRequest.passing}/{pullRequest.checks.length} passed
        </span>
      );
  }
}

function Actions({ pullRequest, onClose, onMerge }: Omit<PullRequestRowProps, "pending">) {
  const [open, setOpen] = useState(false);
  const box = useDismiss<HTMLDivElement>(open, setOpen);

  // Merging is offered while nothing blocks it — an unreviewed pull request counts; the api
  // checks again. Both actions ask before they run.
  const actions = [
    ...(pullRequest.mergeable ? [{ label: "Squash and merge", run: onMerge }] : []),
    { label: "Close without merging", run: onClose },
  ];

  return (
    <div className="acts" ref={box}>
      <button
        type="button"
        className="ghost"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        Actions
        <IconChevron />
      </button>
      {open && (
        <div className="opts" role="menu" aria-label={`Actions for pull request #${pullRequest.number}`}>
          {actions.map((action) => (
            <button
              key={action.label}
              type="button"
              role="menuitem"
              onClick={() => {
                setOpen(false);
                action.run(pullRequest);
              }}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

interface PullRequestRowProps {
  pullRequest: PullRequestView;
  pending: boolean;
  onClose: (pullRequest: PullRequestView) => void;
  onMerge: (pullRequest: PullRequestView) => void;
}

export function PullRequestRow({ pullRequest, pending, onClose, onMerge }: PullRequestRowProps) {
  const verdict = review(pullRequest.reviewState);
  const tone = pullRequest.ready ? " ready" : pullRequest.blocked ? " blocked" : "";

  return (
    <div className={`pr${tone}`}>
      <span className="ico">
        <IconPullRequest />
      </span>
      <div className="t">
        <a href={pullRequest.url} target="_blank" rel="noreferrer">
          {pullRequest.title}
        </a>
        <small>
          <span className="rp">#{pullRequest.number}</span>
          <span>
            {pullRequest.head} → {pullRequest.base}
          </span>
          {pullRequest.author && <span>· {pullRequest.author}</span>}
          {pullRequest.draft && <span className="pill">draft</span>}
        </small>
      </div>
      <span className="age">{ago(pullRequest.updatedAt)}</span>
      <div className="badges">
        <ChecksBadge pullRequest={pullRequest} />
        <span className={`badge ${verdict.tone}`}>{verdict.text}</span>
      </div>
      {pending ? (
        <div className="acts">
          <Spinner label={`Working on #${pullRequest.number}`} />
        </div>
      ) : (
        <Actions pullRequest={pullRequest} onClose={onClose} onMerge={onMerge} />
      )}
    </div>
  );
}
