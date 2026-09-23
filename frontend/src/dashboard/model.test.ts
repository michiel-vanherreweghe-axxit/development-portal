import { describe, expect, it } from "vitest";

import type { Check, PullRequest, ReviewState, WatchRuns } from "../api/types";
import { ago, attentionOf, buildCards, buildPullRequests, review } from "./model";

const pullRequest = (overrides: Partial<PullRequest> = {}): PullRequest => ({
  repository: "acme/api",
  number: 1,
  title: "Add a thing",
  author: "octocat",
  head: "feature",
  base: "main",
  draft: false,
  at: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
  url: "https://github.com/acme/api/pull/1",
  checks: [],
  reviewState: "ReviewRequired",
  error: null,
  ...overrides,
});

const check = (conclusion: string): Check => ({ name: conclusion, conclusion, at: null, url: null });

const watchRuns = (conclusion: string | null, overrides: Partial<WatchRuns> = {}): WatchRuns => ({
  repository: "acme/api",
  workflowFile: "ci.yml",
  displayName: "CI",
  runs: conclusion
    ? [{ conclusion, url: "https://github.com/run/1", at: "2026-01-01T00:00:00Z", number: 1, branch: "main" }]
    : [],
  error: null,
  ...overrides,
});

describe("buildPullRequests", () => {
  it("counts checks and collapses them to one state", () => {
    // Arrange
    const pullRequests = [pullRequest({ checks: [check("success"), check("failure"), check("in_progress")] })];

    // Act
    const [built] = buildPullRequests(pullRequests);

    // Assert
    expect(built).toMatchObject({ passing: 1, failing: 1, running: 1, state: "fail", blocked: true });
  });

  it("offers a merge without a review, but not once changes are requested", () => {
    // Arrange
    const pullRequests = [
      pullRequest({ checks: [check("success")] }),
      pullRequest({ number: 2, reviewState: "ChangesRequested", checks: [check("success")] }),
    ];

    // Act
    const built = buildPullRequests(pullRequests);

    // Assert
    expect(built[0]).toMatchObject({ ready: false, mergeable: true });
    expect(built[1]?.mergeable).toBe(false);
  });

  it("marks an approved pull request with passing checks ready", () => {
    // Arrange
    const pullRequests = [pullRequest({ reviewState: "Approved", checks: [check("success")] })];

    // Act
    const [built] = buildPullRequests(pullRequests);

    // Assert
    expect(built).toMatchObject({ state: "pass", ready: true, blocked: false });
  });

  it("leaves a draft out of ready, and drops rows the api could not read", () => {
    // Arrange
    const pullRequests = [
      pullRequest({ reviewState: "Approved", draft: true, checks: [check("success")] }),
      pullRequest({ number: 2, error: "404 Not Found" }),
    ];

    // Act
    const built = buildPullRequests(pullRequests);

    // Assert
    expect(built).toHaveLength(1);
    expect(built[0]?.ready).toBe(false);
  });

  it("has no checks state when the pull request has no checks", () => {
    // Arrange
    const pullRequests = [pullRequest()];

    // Act
    const [built] = buildPullRequests(pullRequests);

    // Assert
    expect(built?.state).toBe("none");
  });
});

describe("attentionOf", () => {
  it("summarises failing workflows and pull requests, pluralised", () => {
    // Arrange
    const workflows = [watchRuns("failure"), watchRuns("failure", { workflowFile: "release.yml" }), watchRuns("in_progress")];
    const pullRequests = buildPullRequests([
      pullRequest({ checks: [check("failure")] }),
      pullRequest({ number: 2, reviewState: "ChangesRequested" }),
      pullRequest({ number: 3, reviewState: "Approved", checks: [check("success")] }),
    ]);

    // Act
    const attention = attentionOf(workflows, pullRequests);

    // Assert
    expect(attention.any).toBe(true);
    expect(attention.items.map(([, text]) => text)).toEqual([
      "2 workflows failing",
      "1 running",
      "1 PR failing checks",
      "1 changes requested",
      "1 ready to merge",
    ]);
  });

  it("is quiet when everything passed", () => {
    // Arrange
    const workflows = [watchRuns("success"), watchRuns(null)];

    // Act
    const attention = attentionOf(workflows, []);

    // Assert
    expect(attention).toEqual({ items: [], any: false });
  });
});

describe("buildCards", () => {
  it("groups watches per repository and puts the most recent activity first", () => {
    // Arrange
    const rows = [
      watchRuns("success", { repository: "acme/api" }),
      watchRuns("failure", { repository: "acme/api", workflowFile: "release.yml" }),
      watchRuns("success", { repository: "acme/web" }),
    ];
    const pullRequests = buildPullRequests([pullRequest({ repository: "acme/web", updatedAt: "2026-06-01T00:00:00Z" })]);

    // Act
    const cards = buildCards(rows, pullRequests, new Map([["acme/api", null]]));

    // Assert
    expect(cards.map((card) => card.repository)).toEqual(["acme/web", "acme/api"]);
    expect(cards[1]?.workflows).toHaveLength(2);
    expect(cards[1]?.attention.any).toBe(true);
  });
});

describe("ago", () => {
  it("counts down to the coarsest unit that fits", () => {
    // Arrange
    const now = Date.now();

    // Act & Assert
    expect(ago(new Date(now - 5_000).toISOString())).toBe("just now");
    expect(ago(new Date(now - 90_000).toISOString())).toBe("1m ago");
    expect(ago(new Date(now - 7_200_000).toISOString())).toBe("2h ago");
    expect(ago(new Date(now - 259_200_000).toISOString())).toBe("3d ago");
    expect(ago(null)).toBe("—");
  });
});

describe("review", () => {
  it("falls back to review required for a state it does not know", () => {
    // Arrange
    const unknown = "Something" as ReviewState;

    // Act & Assert
    expect(review("ChangesRequested").text).toBe("Changes requested");
    expect(review(unknown).text).toBe("Review required");
  });
});
