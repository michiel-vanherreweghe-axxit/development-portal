const baseUrl = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");

const apiUrl = (path: string) => `${baseUrl}/api/v1${path}`;

export class ApiError extends Error {
  readonly status: number;

  /** The api's own error code, which it sends as the frontend's message key. */
  readonly code: string | null;

  constructor(status: number, message: string, code: string | null = null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

/** The session rides on a cookie, so every call has to carry credentials. */
export async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiUrl(path), { credentials: "include", ...init });

  if (!response.ok) {
    const failure = await readError(response);
    throw new ApiError(response.status, failure.message, failure.code);
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

interface Problem {
  title?: string;
  detail?: string;
  errors?: { code?: string }[];
}

/** The api answers failures as ProblemDetails, carrying an error code where it has one. */
async function readError(response: Response): Promise<{ message: string; code: string | null }> {
  try {
    const problem = (await response.json()) as Problem;
    const code = problem.errors?.[0]?.code ?? null;
    const message = code ?? problem.detail ?? problem.title;

    if (message) {
      return { message, code };
    }
  } catch {
    // A non-json body leaves the status line as the only thing to say.
  }

  return { message: `${response.status} ${response.statusText}`, code: null };
}
