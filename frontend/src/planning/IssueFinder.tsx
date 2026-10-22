import { useEffect, useState } from "react";

import { readIssues } from "../api/portal";
import type { IssueHit, NewPlanItem, Repository } from "../api/types";
import { RepositoryFinder } from "../dashboard/RepositoryFinder";
import { Spinner } from "../ui/Spinner";
import type { PlanTarget } from "./model";
import { TargetSelect } from "./TodoForm";

const DEBOUNCE_MS = 350;

interface IssueFinderProps {
  repositories: Repository[];
  targets: PlanTarget[];
  onAdd: (item: NewPlanItem, hit: IssueHit) => Promise<boolean>;
  onFailure: (failure: unknown, message: string) => void;
}

/** Pick a repository, then type to search its issues; the search api is rate limited, so typing is debounced. */
export function IssueFinder({ repositories, targets, onAdd, onFailure }: IssueFinderProps) {
  const [repository, setRepository] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<IssueHit[]>([]);
  const [searching, setSearching] = useState(false);
  const [chosen, setChosen] = useState<string | null>(null);

  const target = targets.find((candidate) => candidate.value === chosen) ?? targets[0]!;

  useEffect(() => {
    if (!repository) {
      return;
    }

    let current = true;
    const timer = setTimeout(
      () => {
        setSearching(true);
        readIssues(repository, query.trim())
          .then((found) => current && setHits(found))
          .catch((failure: unknown) => {
            if (current) {
              setHits([]);
              onFailure(failure, "Could not search the issues.");
            }
          })
          .finally(() => current && setSearching(false));
      },
      query ? DEBOUNCE_MS : 0,
    );

    return () => {
      current = false;
      clearTimeout(timer);
    };
  }, [repository, query, onFailure]);

  const pick = (name: string) => {
    setRepository(name);
    setQuery("");
    setHits([]);
  };

  return (
    <div className="plan-issues">
      {repository ? (
        <div className="plan-line">
          <span className="plan-repo">
            <code>{repository}</code>
            <button className="drop" aria-label="Pick another repository" onClick={() => setRepository(null)}>
              ✕
            </button>
          </span>
          <input
            value={query}
            placeholder="Search its issues"
            aria-label="Search issues"
            onChange={(event) => setQuery(event.target.value)}
          />
          <TargetSelect targets={targets} value={target.value} onChange={setChosen} label="Plan the issue for" />
        </div>
      ) : (
        <RepositoryFinder
          repositories={repositories}
          watches={[]}
          onPick={pick}
          placeholder={`Search ${repositories.length} repositories to plan an issue from`}
        />
      )}

      {repository && (
        <div className="plan-hits">
          {searching && <Spinner label="Searching issues" />}
          {!searching && hits.length === 0 && <p className="none">No issues match.</p>}
          {hits.map((hit) => (
            <div className="plan-hit" key={hit.number}>
              <div className="wf">
                <b>{hit.title}</b>
                <small>
                  {repository}#{hit.number} · {hit.state}
                </small>
              </div>
              <button
                className="ghost"
                onClick={() =>
                  void onAdd(
                    { kind: "Issue", repository, issueNumber: hit.number, span: target.span, date: target.date },
                    hit,
                  )
                }
              >
                Add to plan
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
