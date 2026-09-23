import type { Repository, WatchRuns, Watches, Workflow } from "../api/types";
import { RepositoryCard } from "./RepositoryCard";
import { RepositoryFinder } from "./RepositoryFinder";
import { RunsPicker } from "./RunsPicker";
import { WorkflowPicker } from "./WorkflowPicker";
import type { PullRequestView, RepositoryCardData } from "./model";

const Skeletons = () => (
  <>
    {[0, 1, 2].map((index) => (
      <div className="skel" key={index} aria-hidden="true">
        <i style={{ width: "38%" }} />
        <i style={{ width: "76%" }} />
        <i style={{ width: "60%" }} />
      </div>
    ))}
    <span className="sr-only" role="status">
      Loading your repositories…
    </span>
  </>
);

interface RepositoriesPageProps {
  repositories: Repository[];
  watches: Watches;
  cards: RepositoryCardData[];
  shown: RepositoryCardData[];
  hotCount: number;
  pullsReady: boolean;
  loaded: boolean;
  pending: string | null;
  onlyAttention: boolean;
  onToggleAttention: () => void;
  picked: string | null;
  onPick: (repository: string | null) => void;
  onWatch: (repository: string, workflow: Workflow) => void;
  onDropWatch: (watch: WatchRuns) => void;
  onDropRepository: (repository: string, workflows: number) => void;
  onClosePullRequest: (pullRequest: PullRequestView) => void;
  onMergePullRequest: (pullRequest: PullRequestView) => void;
  onRunLimit: (limit: number) => void;
}

export function RepositoriesPage({
  repositories,
  watches,
  cards,
  shown,
  hotCount,
  pullsReady,
  loaded,
  pending,
  onlyAttention,
  onToggleAttention,
  picked,
  onPick,
  onWatch,
  onDropWatch,
  onDropRepository,
  onClosePullRequest,
  onMergePullRequest,
  onRunLimit,
}: RepositoriesPageProps) {
  const pickerFor = (repository: string, bare = true) => (
    <WorkflowPicker
      key={repository}
      repository={repository}
      watches={watches.watches}
      bare={bare}
      adding={pending?.startsWith(`${repository}/`) ? pending.slice(repository.length + 1) : null}
      onAdd={(workflow) => onWatch(repository, workflow)}
      onClose={() => onPick(null)}
    />
  );

  const watched = new Set(cards.map((card) => card.repository));

  return (
    <>
      <div className="toolbar">
        <button className="fchip" aria-pressed={onlyAttention} onClick={onToggleAttention}>
          Needs attention only<b>{hotCount}</b>
        </button>
        <RunsPicker value={watches.runLimit} onPick={onRunLimit} />
      </div>

      <RepositoryFinder repositories={repositories} watches={watches.watches} onPick={onPick} />
      {picked && !watched.has(picked) && pickerFor(picked, false)}

      {!loaded && <Skeletons />}
      {loaded && cards.length === 0 && (
        <p className="empty-state">
          Nothing watched yet — press <span className="kbd">/</span> and search for a repository.
        </p>
      )}
      {loaded && cards.length > 0 && shown.length === 0 && (
        <p className="empty-state">Nothing needs attention right now.</p>
      )}

      <div className="cards" style={{ "--runs": watches.runLimit } as React.CSSProperties}>
        {shown.map((card) => (
          <RepositoryCard
            key={card.repository}
            card={card}
            limit={watches.runLimit}
            pending={pending}
            pullsReady={pullsReady}
            picker={picked === card.repository ? pickerFor(card.repository) : null}
            onAddTo={onPick}
            onDrop={onDropWatch}
            onDropRepository={onDropRepository}
            onClosePullRequest={onClosePullRequest}
            onMergePullRequest={onMergePullRequest}
          />
        ))}
      </div>
    </>
  );
}
