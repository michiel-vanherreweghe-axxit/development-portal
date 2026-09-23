import { useState, type FormEvent } from "react";

import { ApiError } from "../api/client";
import type { CurrentUser } from "../api/types";
import { IconGitHub } from "../shell/icons";
import { Spinner } from "../ui/Spinner";

const TOKEN_SETTINGS_URL = "https://github.com/settings/tokens";

const MESSAGES: Record<string, string> = {
  "SignIn.TokenInvalid": "GitHub did not accept that token. It must be valid and carry the repo scope.",
  "SignIn.TokenRequired": "Paste a personal access token first.",
  "GitHub.RequestFailed": "GitHub could not be reached. The token was not checked — try again.",
};

const message = (failure: unknown) => {
  if (failure instanceof ApiError) {
    // A problem without an errors array carries its code as the message.
    return MESSAGES[failure.code ?? failure.message] ?? failure.message;
  }

  return failure instanceof Error ? failure.message : "The token could not be checked.";
};

interface SettingsPageProps {
  user: CurrentUser | null;
  /** Whatever went wrong while reading the session on load, if anything. */
  probeError: string | null;
  onConnect: (personalAccessToken: string) => Promise<void>;
  onDisconnect: () => void;
}

export function SettingsPage({ user, probeError, onConnect, onDisconnect }: SettingsPageProps) {
  const [token, setToken] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);

    onConnect(token.trim())
      .catch((failure: unknown) => {
        setError(message(failure));
        setBusy(false);
      });
  };

  return (
    <div className="group settings">
      <header>
        <span className="repo">GitHub connection</span>
      </header>

      {user ? (
        <div className="body">
          <div className="account">
            {user.avatarUrl && <img src={user.avatarUrl} alt="" />}
            <div>
              <b>{user.login}</b>
              <small>Connected — the portal reads GitHub as this account.</small>
            </div>
            <button className="ghost" onClick={onDisconnect}>
              Disconnect
            </button>
          </div>
          <p className="help">
            The token is kept in your session cookie, never in the portal's database. Disconnecting
            forgets it, and so does signing out of this browser for two weeks.
          </p>
        </div>
      ) : (
        <form className="body" onSubmit={submit}>
          <label className="field" htmlFor="pat">
            <span>Personal access token</span>
            <input
              id="pat"
              type="password"
              value={token}
              autoComplete="off"
              spellCheck={false}
              placeholder="ghp_… or github_pat_…"
              onChange={(event) => setToken(event.target.value)}
            />
          </label>
          <p className="help">
            A classic token needs the <code>repo</code> scope to read workflow runs and pull requests
            on private repositories; a fine-grained token needs read access to Actions and metadata, and
            read and write access to pull requests to close or merge them. Create one under{" "}
            <a href={TOKEN_SETTINGS_URL} target="_blank" rel="noreferrer">
              github.com/settings/tokens ↗
            </a>
            .
          </p>
          {(error ?? probeError) && <p className="err">{error ?? probeError}</p>}
          <button className="btn" type="submit" disabled={busy || token.trim().length === 0}>
            {busy ? <Spinner label="Connecting" /> : <IconGitHub />}
            Connect GitHub
          </button>
        </form>
      )}
    </div>
  );
}
