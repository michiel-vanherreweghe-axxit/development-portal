import type { ReactNode } from "react";

import type { WatchRuns } from "../api/types";
import { Spinner } from "../ui/Spinner";
import { PullRequestRow } from "./PullRequestRow";
import { WorkflowRow } from "./WorkflowRow";
import { cardId, pullRequestKey, watchKey, type PullRequestView, type RepositoryCardData } from "./model";

interface RepositoryCardProps {
  card: RepositoryCardData;
  limit: number;
  /** The watch key or repository currently being saved, if any. */
  pending: string | null;
  pullsReady: boolean;
  picker: ReactNode;
  onAddTo: (repository: string) => void;
  onDrop: (watch: WatchRuns) => void;
  onDropRepository: (repository: string, workflows: number) => void;
  onClosePullRequest: (pullRequest: PullRequestView) => void;
  onMergePullRequest: (pullRequest: PullRequestView) => void;
}

export function RepositoryCard({
  card,
  limit,
  pending,
  pullsReady,
  picker,
  onAddTo,
  onDrop,
  onDropRepository,
  onClosePullRequest,
  onMergePullRequest,
}: RepositoryCardProps) {
  const { repository, workflows, pullRequests, attention } = card;
  const [owner, name] = repository.split("/");
  const dropping = pending === repository;

  return (
    <div className={`group card${dropping ? " pending" : ""}`} id={cardId(repository)}>
      <header>
        <a className="repo" href={`https://github.com/${repository}`} target="_blank" rel="noreferrer">
          <span className="owner">{owner}/</span>
          {name}
        </a>
        <span className="sum">
          {attention.any ? (
            attention.items.map(([tone, text]) => (
              <span key={text} className={`badge ${tone}`}>
                {text}
              </span>
            ))
          ) : (
            <span className="calm">all clear</span>
          )}
        </span>
        {dropping ? (
          <Spinner label={`Removing ${repository}`} />
        ) : (
          <button
            className="drop unwatch"
            onClick={() => onDropRepository(repository, workflows.length)}
            title={`Stop watching ${repository}`}
            aria-label={`Stop watching all of ${repository}`}
          >
            ✕
          </button>
        )}
      </header>

      <div className="cols">
        <div className="col">
          <h3>
            Workflows <span>{workflows.length}</span>
            <button className="addwf" onClick={() => onAddTo(repository)}>
              + Add workflow
            </button>
          </h3>
          {workflows.map((watch) => (
            <WorkflowRow
              key={watch.workflowFile}
              watch={watch}
              limit={limit}
              pending={dropping || pending === watchKey(watch)}
              onDrop={onDrop}
            />
          ))}
          {picker}
        </div>

        <div className="col">
          <h3>
            Pull requests <span>{pullsReady ? pullRequests.length : "—"}</span>
            <a href={`https://github.com/${repository}/pulls`} target="_blank" rel="noreferrer">
              all on GitHub ↗
            </a>
          </h3>
          {!pullsReady ? (
            <p className="none">
              <Spinner label="Loading" /> Loading pull requests…
            </p>
          ) : pullRequests.length ? (
            [...pullRequests]
              .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
              .map((pullRequest) => (
                <PullRequestRow
                  key={pullRequest.url}
                  pullRequest={pullRequest}
                  pending={pending === pullRequestKey(pullRequest)}
                  onClose={onClosePullRequest}
                  onMerge={onMergePullRequest}
                />
              ))
          ) : (
            <p className="none">No open pull requests</p>
          )}
        </div>
      </div>
    </div>
  );
}
