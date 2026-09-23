import type { PullRequest, ReviewState, Run, Watch, WatchRuns } from "../api/types";

/** GitHub's conclusions, grouped the way the dashboard colours them. */
export const BAD = ["failure", "timed_out", "startup_failure"];
export const BUSY = ["in_progress", "queued", "requested", "waiting"];

const LABELS: Record<string, string> = {
  success: "passed",
  failure: "failed",
  timed_out: "timed out",
  startup_failure: "start failed",
  in_progress: "running",
  queued: "queued",
  cancelled: "cancelled",
  skipped: "skipped",
  neutral: "neutral",
};

export const label = (conclusion: string) => LABELS[conclusion] ?? conclusion;

/** The anchor the command palette scrolls to. */
export const cardId = (repository: string) => `g-${repository.replace("/", "-")}`;

export const watchKey = (watch: Watch) => `${watch.repository}/${watch.workflowFile}`;

export const pullRequestKey = (pullRequest: PullRequest) => `${pullRequest.repository}#${pullRequest.number}`;

export const watchName = (watch: Watch) => watch.displayName ?? watch.workflowFile;

export const ago = (iso: string | null) => {
  if (!iso) {
    return "—";
  }

  const seconds = (Date.now() - new Date(iso).getTime()) / 1000;
  for (const [size, unit] of [
    [86400, "d"],
    [3600, "h"],
    [60, "m"],
  ] as const) {
    if (seconds >= size) {
      return `${Math.floor(seconds / size)}${unit} ago`;
    }
  }

  return "just now";
};

export const REVIEW: Record<string, { tone: "pass" | "fail" | "idle"; text: string }> = {
  approved: { tone: "pass", text: "Approved" },
  changesrequested: { tone: "fail", text: "Changes requested" },
  reviewrequired: { tone: "idle", text: "Review required" },
  undefined: { tone: "idle", text: "Review required" },
};

export const review = (state: ReviewState) => REVIEW[state.toLowerCase()] ?? REVIEW.reviewrequired!;

export type ChecksState = "none" | "fail" | "run" | "pass";

export interface PullRequestView extends PullRequest {
  failing: number;
  running: number;
  passing: number;
  state: ChecksState;
  ready: boolean;
  /** What the api lets through: green, and nobody asking for changes. A review is not required. */
  mergeable: boolean;
  blocked: boolean;
}

/** Check runs and review verdicts as the api returned them, counted per pull request. */
export function buildPullRequests(pullRequests: PullRequest[]): PullRequestView[] {
  return pullRequests
    .filter((pullRequest) => !pullRequest.error)
    .map((pullRequest) => {
      const failing = pullRequest.checks.filter((check) => BAD.includes(check.conclusion)).length;
      const running = pullRequest.checks.filter((check) => BUSY.includes(check.conclusion)).length;
      const passing = pullRequest.checks.filter((check) => check.conclusion === "success").length;
      const state: ChecksState = !pullRequest.checks.length
        ? "none"
        : failing
          ? "fail"
          : running
            ? "run"
            : "pass";

      return {
        ...pullRequest,
        failing,
        running,
        passing,
        state,
        ready: !pullRequest.draft && pullRequest.reviewState === "Approved" && state === "pass",
        mergeable: !pullRequest.draft && pullRequest.reviewState !== "ChangesRequested" && state === "pass",
        blocked: pullRequest.reviewState === "ChangesRequested" || state === "fail",
      };
    });
}

export const latestRun = (watch: WatchRuns): Run | undefined => watch.runs[0];

/**
 * What needs attention in one repository: failing or running workflows, and pull requests
 * with failing checks, changes requested, or approved and ready to merge.
 */
export function attentionOf(workflows: WatchRuns[], pullRequests: PullRequestView[]) {
  const counted = (n: number, singular: string, plural = `${singular}s`) =>
    `${n} ${n > 1 ? plural : singular}`;

  const workflowsFailing = workflows.filter((w) => BAD.includes(latestRun(w)?.conclusion ?? "")).length;
  const workflowsRunning = workflows.filter((w) => BUSY.includes(latestRun(w)?.conclusion ?? "")).length;
  const checksFailing = pullRequests.filter((p) => p.state === "fail").length;
  const changesRequested = pullRequests.filter((p) => p.reviewState === "ChangesRequested").length;
  const readyToMerge = pullRequests.filter((p) => p.ready).length;

  const items: [tone: "fail" | "run" | "pass", text: string][] = [];
  if (workflowsFailing) {
    items.push(["fail", `${counted(workflowsFailing, "workflow")} failing`]);
  }
  if (workflowsRunning) {
    items.push(["run", `${workflowsRunning} running`]);
  }
  if (checksFailing) {
    items.push(["fail", `${counted(checksFailing, "PR")} failing checks`]);
  }
  if (changesRequested) {
    items.push(["fail", `${changesRequested} changes requested`]);
  }
  if (readyToMerge) {
    items.push(["pass", `${readyToMerge} ready to merge`]);
  }

  return { items, any: items.length > 0 };
}

export interface RepositoryCardData {
  repository: string;
  workflows: WatchRuns[];
  pullRequests: PullRequestView[];
  activity: string;
  attention: ReturnType<typeof attentionOf>;
}

/** One card per watched repository, most recently active first. */
export function buildCards(
  rows: WatchRuns[],
  pullRequests: PullRequestView[],
  pushedAt: Map<string, string | null>,
): RepositoryCardData[] {
  const byRepository = new Map<string, WatchRuns[]>();
  for (const row of rows) {
    byRepository.set(row.repository, [...(byRepository.get(row.repository) ?? []), row]);
  }

  return [...byRepository]
    .map(([repository, workflows]) => {
      const owned = pullRequests.filter((p) => p.repository === repository);
      const activity = [
        pushedAt.get(repository) ?? "",
        ...workflows.map((w) => latestRun(w)?.at ?? ""),
        ...owned.map((p) => p.updatedAt),
      ]
        .sort()
        .pop()!;

      return { repository, workflows, pullRequests: owned, activity, attention: attentionOf(workflows, owned) };
    })
    .sort((a, b) => b.activity.localeCompare(a.activity));
}
