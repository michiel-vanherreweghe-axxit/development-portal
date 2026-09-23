/** Mirrors the dtos the portal api answers with; property names follow its camelCase json. */

export interface CurrentUser {
  gitHubId: number;
  login: string;
  avatarUrl: string | null;
}

export interface Repository {
  fullName: string;
  private: boolean;
  pushedAt: string | null;
}

export interface Workflow {
  name: string;
  file: string;
}

export interface Watch {
  repository: string;
  workflowFile: string;
  displayName: string | null;
}

export interface Watches {
  runLimit: number;
  watches: Watch[];
}

/** A run that is still going has no conclusion yet, so its status stands in. */
export interface Run {
  conclusion: string;
  url: string;
  at: string;
  number: number;
  branch: string | null;
}

export interface WatchRuns extends Watch {
  runs: Run[];
  /** One unreadable repository marks its own row instead of failing the dashboard. */
  error: string | null;
}

export interface Check {
  name: string;
  conclusion: string;
  at: string | null;
  url: string | null;
}

export type ReviewState = "ReviewRequired" | "Approved" | "ChangesRequested" | "Undefined";

export interface PullRequest {
  repository: string;
  number: number;
  title: string;
  author: string | null;
  head: string;
  base: string;
  draft: boolean;
  at: string;
  updatedAt: string;
  url: string;
  checks: Check[];
  reviewState: ReviewState;
  error: string | null;
}
