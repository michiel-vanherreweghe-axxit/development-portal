import { request } from "./client";
import type {
  CurrentUser,
  IssueHit,
  NewPlanItem,
  PlanItem,
  PlanItemChange,
  PullRequest,
  Repository,
  WatchRuns,
  Watches,
  Workflow,
} from "./types";

/**
 * Hands GitHub a personal access token. The api validates it against GitHub and keeps it in the
 * session cookie, so it is never stored anywhere else.
 */
export const connectGitHub = (personalAccessToken: string) =>
  request<CurrentUser>("/auth/sign-in", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ personalAccessToken }),
  });

export const readCurrentUser = () => request<CurrentUser>("/auth/me");

/** Forgets the token by dropping the session cookie. */
export const disconnectGitHub = () => request<void>("/auth/sign-in", { method: "DELETE" });

export const readRepositories = () => request<Repository[]>("/repositories");

export const readWorkflows = (repository: string) =>
  request<Workflow[]>(`/workflows?repository=${encodeURIComponent(repository)}`);

export const readWatches = () => request<Watches>("/watches");

export const updateWatches = (watches: Watches) =>
  request<Watches>("/watches", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(watches),
  });

export const readRuns = () => request<WatchRuns[]>("/runs");

export const readPullRequests = () => request<PullRequest[]>("/pull-requests");

/** The repository is `owner/name`, which is how the route spells it too. */
const act = (repository: string, number: number, action: "close" | "merge") =>
  request<void>(`/pull-requests/${repository}/${number}/${action}`, { method: "POST" });

export const closePullRequest = (repository: string, number: number) => act(repository, number, "close");

export const mergePullRequest = (repository: string, number: number) => act(repository, number, "merge");

const json = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

/** Dates are `yyyy-MM-dd`; the api answers the weeks they touch. */
export const readPlanItems = (from: string, to: string) => request<PlanItem[]>(`/plan-items?from=${from}&to=${to}`);

export const createPlanItem = (item: NewPlanItem) => request<PlanItem>("/plan-items", json("POST", item));

export const updatePlanItem = (id: number, change: PlanItemChange) =>
  request<PlanItem>(`/plan-items/${id}`, json("PUT", change));

export const deletePlanItem = (id: number) => request<void>(`/plan-items/${id}`, { method: "DELETE" });

export const readIssues = (repository: string, query: string) =>
  request<IssueHit[]>(`/issues?repository=${encodeURIComponent(repository)}&query=${encodeURIComponent(query)}`);
