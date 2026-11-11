/** A single entry in a Scoreboardly leaderboard. */
export interface ScoreEntry {
  /** Display name supplied for the score. */
  playerName: string;
  /** Non-negative score value. */
  score: number;
}

/** Public shape returned by a Scoreboardly leaderboard read. */
export interface Scoreboard {
  id?: string;
  name?: string;
  maximumScores: number;
  scores: ScoreEntry[];
}

/** Data required to submit a score. */
export interface ScoreSubmission {
  playerName: string;
  score: number;
}

/**
 * Supplies a short-lived access token to the SDK.
 *
 * Browser applications should obtain this token from their own trusted,
 * same-origin backend. A permanent Scoreboardly game credential must never
 * be returned to browser code.
 */
export interface AuthProvider {
  getAccessToken(): Promise<string | null>;
}

/** Configuration for a Scoreboardly client instance. */
export interface ScoreboardlyOptions {
  /** Scoreboardly API root, including its /api path. */
  baseUrl: string;
  /** Public identifier of the board this client will access. */
  scoreboardId: string;
  /** Provider for short-lived, board-scoped access tokens. */
  auth: AuthProvider;
  /** Injectable fetch implementation for tests and non-browser runtimes. */
  fetch?: typeof fetch;
}

/** Stable categories for errors consumers may need to handle. */
export type ScoreboardlyErrorCode =
  | "network"
  | "unauthorized"
  | "forbidden"
  | "conflict"
  | "http"
  | "invalid_response";

/** Error raised for failed Scoreboardly requests or invalid responses. */
export class ScoreboardlyError extends Error {
  public readonly code: ScoreboardlyErrorCode;
  public readonly status?: number;

  public constructor(
    code: ScoreboardlyErrorCode,
    message: string,
    status?: number
  ) {
    super(message);
    this.name = "ScoreboardlyError";
    this.code = code;
    this.status = status;
  }
}

/** Public methods exposed by a configured Scoreboardly client. */
export interface ScoreboardlyClient {
  /** Retrieves the latest public leaderboard snapshot. */
  getLeaderboard(): Promise<Scoreboard>;
  /** Submits a score using the token supplied by the configured auth provider. */
  submitScore(submission: ScoreSubmission): Promise<void>;
}

function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim().replace(/\/+$/, "");
  if (!trimmed) {
    throw new TypeError("Scoreboardly baseUrl is required.");
  }

  return trimmed;
}

function parseJson(text: string): unknown {
  try {
    const parsed: unknown = JSON.parse(text);
    // The current .NET endpoint serializes its DTO before returning it.
    // Accept both a normal JSON object and that legacy JSON-string envelope.
    return typeof parsed === "string" ? JSON.parse(parsed) as unknown : parsed;
  } catch {
    throw new ScoreboardlyError(
      "invalid_response",
      "Scoreboardly returned a response that was not valid JSON."
    );
  }
}

function parseScoreboard(payload: unknown): Scoreboard {
  if (typeof payload !== "object" || payload === null) {
    throw new ScoreboardlyError(
      "invalid_response",
      "Scoreboardly returned an invalid leaderboard payload."
    );
  }

  const candidate = payload as Partial<Scoreboard>;
  if (
    typeof candidate.maximumScores !== "number" ||
    !Array.isArray(candidate.scores)
  ) {
    throw new ScoreboardlyError(
      "invalid_response",
      "Leaderboard payload is missing maximumScores or scores."
    );
  }

  const scores = candidate.scores.filter((entry): entry is ScoreEntry => {
    if (typeof entry !== "object" || entry === null) {
      return false;
    }

    const score = entry as Partial<ScoreEntry>;
    return typeof score.playerName === "string" && typeof score.score === "number";
  });

  return {
    ...(typeof candidate.id === "string" ? { id: candidate.id } : {}),
    ...(typeof candidate.name === "string" ? { name: candidate.name } : {}),
    maximumScores: candidate.maximumScores,
    scores
  };
}

function errorForResponse(status: number): ScoreboardlyError {
  if (status === 401) {
    return new ScoreboardlyError("unauthorized", "Scoreboardly authentication is missing, invalid, or expired.", status);
  }
  if (status === 403) {
    return new ScoreboardlyError("forbidden", "The access token is not authorized for this board or operation.", status);
  }
  if (status === 409) {
    return new ScoreboardlyError("conflict", "The submitted score did not qualify for this leaderboard.", status);
  }
  return new ScoreboardlyError("http", `Scoreboardly request failed with HTTP ${status}.`, status);
}

/**
 * Creates a small, framework-independent client facade.
 *
 * The fetch-based transport is intentionally kept thin in this first slice.
 * Token lifecycle, refresh coordination, richer response validation, and
 * transport policies are built out in the following SDK issue.
 */
export function createScoreboardly(options: ScoreboardlyOptions): ScoreboardlyClient {
  const baseUrl = normalizeBaseUrl(options.baseUrl);
  const boardId = encodeURIComponent(options.scoreboardId);
  const fetcher = options.fetch ?? globalThis.fetch;

  if (!options.scoreboardId.trim()) {
    throw new TypeError("Scoreboardly scoreboardId is required.");
  }

  if (typeof fetcher !== "function") {
    throw new TypeError("A fetch implementation is required.");
  }

  async function send(path: string, init?: RequestInit): Promise<Response> {
    try {
      const response = await fetcher(`${baseUrl}/scoreboards/${boardId}${path}`, init);
      if (!response.ok) {
        throw errorForResponse(response.status);
      }
      return response;
    } catch (error) {
      if (error instanceof ScoreboardlyError) {
        throw error;
      }
      throw new ScoreboardlyError(
        "network",
        "Could not reach Scoreboardly. Check the connection and try again."
      );
    }
  }

  return {
    async getLeaderboard(): Promise<Scoreboard> {
      const response = await send("");
      return parseScoreboard(parseJson(await response.text()));
    },

    async submitScore(submission: ScoreSubmission): Promise<void> {
      const token = await options.auth.getAccessToken();
      if (!token) {
        throw new ScoreboardlyError(
          "unauthorized",
          "The authentication provider did not supply an access token.",
          401
        );
      }

      await send("", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${token}`
        },
        body: JSON.stringify(submission)
      });
    }
  };
}
