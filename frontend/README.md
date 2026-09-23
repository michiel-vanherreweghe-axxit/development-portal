# DevPortal frontend

React + TypeScript + Vite. It talks to the portal api over `/api/v1`. The dashboard opens
without signing in; GitHub is connected from the Settings page with a personal access token,
which the api validates and keeps in the session cookie.

## Running

With the Aspire app host — starts the api and this dev server together, and points this app at
the api it just started:

```bash
cd ../backend && dotnet run --project src/Host
```

On its own, against an api that is already running:

```bash
cp .env.example .env   # set VITE_API_URL to that api
npm install && npm run dev
```

The dev server keeps port 5173: the api trusts exactly one frontend origin and answers
`Portal:FrontendUri` with it.

## Checks

```bash
npm run lint    # eslint
npm run test    # vitest, pure dashboard logic
npm run build   # tsc --build + vite build
```
