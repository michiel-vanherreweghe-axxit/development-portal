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

export type PlanSpan = "Day" | "Week";

export type PlanItemKind = "Todo" | "Issue";

/** What GitHub says about a planned issue right now; the stored title is the fallback for the last two. */
export type IssueState = "Open" | "Closed" | "Unavailable" | "Unknown";

/** `date` is a plain `yyyy-MM-dd` day, or for a weekly entry the Monday of its week. */
export interface PlanItem {
  id: number;
  kind: PlanItemKind;
  title: string;
  description: string | null;
  /** `owner/name`, for an issue entry. */
  repository: string | null;
  issueNumber: number | null;
  /** Null for a to-do, and for an issue entry whose state was not read in that response. */
  issueState: IssueState | null;
  issueUrl: string | null;
  span: PlanSpan;
  date: string;
  isCompleted: boolean;
  completedAt: string | null;
}

export interface IssueHit {
  number: number;
  title: string;
  state: string;
  url: string;
}

/** Where a new entry goes; a to-do carries text, an issue entry a reference. */
export interface NewPlanItem {
  kind: PlanItemKind;
  title?: string;
  description?: string;
  repository?: string;
  issueNumber?: number;
  span: PlanSpan;
  date: string;
}

/** The whole editable state of an entry: moving or completing it sends the rest unchanged. */
export interface PlanItemChange {
  title?: string | null;
  description?: string | null;
  span: PlanSpan;
  date: string;
  isCompleted: boolean;
}
