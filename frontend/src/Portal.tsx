import { useCallback, useMemo, useState } from "react";

import { closePullRequest, mergePullRequest } from "./api/portal";
import type { Session } from "./auth/useSession";
import type { WatchRuns, Workflow } from "./api/types";
import { RepositoriesPage } from "./dashboard/RepositoriesPage";
import {
  BAD,
  buildCards,
  buildPullRequests,
  cardId,
  latestRun,
  pullRequestErrors,
  pullRequestKey,
  watchKey,
  watchName,
  type PullRequestView,
} from "./dashboard/model";
import { usePortalData } from "./dashboard/usePortalData";
import { PlanningPage } from "./planning/PlanningPage";
import { SettingsPage } from "./settings/SettingsPage";
import { IconGear, IconPlan, IconRepo } from "./shell/icons";
import { Shell } from "./shell/Shell";
import type { Command, PageDefinition } from "./shell/types";
import { Confirm, type Question } from "./ui/Confirm";
import { Toasts } from "./ui/Toasts";
import { useToasts } from "./ui/useToasts";

const NotConnected = () => (
  <p className="empty-state">
    GitHub is not connected yet — open <a href="#settings">Settings</a> and paste a personal access
    token to see your repositories.
  </p>
);

export function Portal({ session }: { session: Session }) {
  const { toasts, notify } = useToasts();
  const connected = session.user !== null;
  const data = usePortalData(notify, session.expired, connected);
  const [onlyAttention, setOnlyAttention] = useState(false);
  const [picked, setPicked] = useState<string | null>(null);
  const [ask, setAsk] = useState<Question | null>(null);

  const pullRequests = useMemo(() => buildPullRequests(data.pullRequests ?? []), [data.pullRequests]);
  const pushedAt = useMemo(
    () => new Map(data.repositories.map((repository) => [repository.fullName, repository.pushedAt])),
    [data.repositories],
  );
  const errors = useMemo(() => pullRequestErrors(data.pullRequests ?? []), [data.pullRequests]);
  const cards = useMemo(
    () => buildCards(data.rows, pullRequests, pushedAt, errors),
    [data.rows, pullRequests, pushedAt, errors],
  );
  const closeAsk = useCallback(() => setAsk(null), []);

  const hot = cards.filter((card) => card.attention.any);
  const shown = onlyAttention ? hot : cards;
  const failing = data.rows.filter(
    (row) => !row.error && BAD.includes(latestRun(row)?.conclusion ?? ""),
  ).length;

  const watch = (repository: string, workflow: Workflow) =>
    void data.commit(
      (current) => ({
        ...current,
        watches: [...current.watches, { repository, workflowFile: workflow.file, displayName: workflow.name }],
      }),
      `Now watching ${workflow.name} in ${repository}.`,
      `${repository}/${workflow.file}`,
    );

  const dropWatch = (row: WatchRuns) =>
    setAsk({
      title: "Stop watching this workflow?",
      detail: (
        <>
          <code>{watchName(row)}</code> in <code>{row.repository}</code> disappears from the dashboard. You
          can add it back from the search at any time.
        </>
      ),
      verb: "Stop watching",
      onYes: () =>
        void data.commit(
          (current) => ({
            ...current,
            watches: current.watches.filter((candidate) => watchKey(candidate) !== watchKey(row)),
          }),
          `Stopped watching ${watchName(row)}.`,
          watchKey(row),
        ),
    });

  const dropRepository = (repository: string, workflows: number) =>
    setAsk({
      title: "Stop watching this repository?",
      detail: (
        <>
          All {workflows} watched workflow{workflows > 1 ? "s" : ""} in <code>{repository}</code> disappear
          from the dashboard. Its pull requests go too. You can add them back from the search at any time.
        </>
      ),
      verb: "Stop watching",
      onYes: () =>
        void data.commit(
          (current) => ({
            ...current,
            watches: current.watches.filter((candidate) => candidate.repository !== repository),
          }),
          `Stopped watching ${repository} — ${workflows} workflow${workflows > 1 ? "s" : ""} removed.`,
          repository,
        ),
    });

  const closePull = (pullRequest: PullRequestView) =>
    setAsk({
      title: "Close this pull request?",
      detail: (
        <>
          <code>#{pullRequest.number}</code> in <code>{pullRequest.repository}</code> is closed without
          merging. Anyone can reopen it on GitHub.
        </>
      ),
      verb: "Close it",
      cancel: "Leave it open",
      onYes: () =>
        void data.act(
          () => closePullRequest(pullRequest.repository, pullRequest.number),
          `Closed #${pullRequest.number} in ${pullRequest.repository}.`,
          pullRequestKey(pullRequest),
          { "GitHub.Refused": "GitHub would not close that pull request." },
        ),
    });

  const mergePull = (pullRequest: PullRequestView) =>
    setAsk({
      title: "Squash and merge this pull request?",
      detail: (
        <>
          <code>#{pullRequest.number}</code> lands as one commit on <code>{pullRequest.base}</code> in{" "}
          <code>{pullRequest.repository}</code>; the history of <code>{pullRequest.head}</code> is squashed
          away. This cannot be undone from the portal.
          {pullRequest.reviewState !== "Approved" && " Nobody has approved it yet."}
        </>
      ),
      verb: "Squash and merge",
      cancel: "Not now",
      onYes: () =>
        void data.act(
          () => mergePullRequest(pullRequest.repository, pullRequest.number),
          `Squashed and merged #${pullRequest.number} in ${pullRequest.repository}.`,
          pullRequestKey(pullRequest),
          {
            "PullRequest.NotReady": "That pull request is no longer ready to merge — refresh to see why.",
            "GitHub.Refused":
              "GitHub refused the merge: conflicts, a protection rule, squash merging turned off, or the branch moved on.",
          },
        ),
    });

  const pages: PageDefinition[] = [
    {
      id: "repositories",
      title: "Repositories",
      hint: "Workflow runs and pull requests per repository",
      icon: <IconRepo />,
      badge: failing || null,
      render: () =>
        !connected ? (
          <NotConnected />
        ) : (
          <RepositoriesPage
            repositories={data.repositories}
            watches={data.watches}
            cards={cards}
            shown={shown}
            hotCount={hot.length}
            pullsReady={data.pullRequests !== null}
            loaded={data.updatedAt !== null}
            pending={data.pending}
            onlyAttention={onlyAttention}
            onToggleAttention={() => setOnlyAttention((current) => !current)}
            picked={picked}
            onPick={setPicked}
            onWatch={watch}
            onDropWatch={dropWatch}
            onDropRepository={dropRepository}
            onClosePullRequest={closePull}
            onMergePullRequest={mergePull}
            onRunLimit={(runLimit) =>
              void data.commit((current) => ({ ...current, runLimit }), `Showing the last ${runLimit} runs.`, null)
            }
          />
        ),
    },
    {
      id: "planning",
      title: "Planning",
      hint: "Plan your days and weeks from issues and to-dos",
      icon: <IconPlan />,
      group: "Planning",
      render: () =>
        !connected ? (
          <NotConnected />
        ) : (
          <PlanningPage repositories={data.repositories} notify={notify} onUnauthorized={session.expired} />
        ),
    },
    {
      id: "settings",
      title: "Settings",
      hint: "Connect the GitHub account the portal reads with",
      icon: <IconGear />,
      group: "Portal",
      render: () => (
        <SettingsPage
          user={session.user}
          probeError={session.error}
          onConnect={session.connect}
          onDisconnect={() => void session.disconnect()}
        />
      ),
    },
  ];

  const commands: Command[] = connected
    ? [
        ...cards.map((card) => ({
          group: "Repositories",
          label: card.repository,
          icon: <IconRepo />,
          hint: card.attention.any ? "needs attention" : "all clear",
          run: () => document.getElementById(cardId(card.repository))?.scrollIntoView({ behavior: "smooth", block: "start" }),
        })),
        {
          group: "Actions",
          label: "Watch a workflow",
          hint: "/",
          run: () => {
            const field = document.getElementById("repo-search");
            field?.focus();
            field?.scrollIntoView({ block: "center" });
          },
        },
        {
          group: "Actions",
          label: onlyAttention ? "Show all repositories" : "Show only what needs attention",
          run: () => setOnlyAttention((current) => !current),
        },
        { group: "Actions", label: "Refresh now", run: () => void data.refresh() },
      ]
    : [];

  const status = !connected ? null : (
    <>
      <span>
        {data.busy
          ? "refreshing…"
          : data.updatedAt
            ? `updated ${data.updatedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`
            : "loading…"}
      </span>
      <button
        className="ghost"
        disabled={data.busy}
        onClick={() => void data.refresh().then(() => notify("Dashboard updated.", "info"))}
      >
        {data.busy ? "Refreshing…" : "Refresh"}
      </button>
    </>
  );

  return (
    <>
      <Shell
        pages={pages}
        commands={commands}
        status={status}
        busy={data.busy}
        user={session.user}
        fallbackPage={connected ? "repositories" : "settings"}
      />
      <Confirm ask={ask} onClose={closeAsk} />
      <Toasts items={toasts} />
    </>
  );
}
