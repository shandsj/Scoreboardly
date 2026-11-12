/** A single entry in a Scoreboardly leaderboard. */
export interface ScoreEntry {
  /** Display name supplied for the score. */
  playerName: string;
  /** Non-negative score value. */
  score: number;
}

/** Public shape returned by a Scoreboardly leaderboard read. */
export interface Scoreboard {
  /** Board identifier, when included by the API. */
  id?: string;
  /** Human-readable board name, when included by the API. */
  name?: string;
  /** Maximum number of entries the board is configured to retain. */
  maximumScores: number;
  /** Valid score entries in API-defined order. */
  scores: ScoreEntry[];
}

/** Data required to submit a score. */
export interface ScoreSubmission {
  /** Player display name. Server-side validation remains authoritative. */
  playerName: string;
  /** Score value. Server-side validation and qualification remain authoritative. */
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
  /**
   * Obtains a short-lived token scoped to the configured board and operation.
   * @returns A bearer token, or null when submission is not authorized.
   * @remarks Provider errors currently propagate to the caller unchanged.
   */
  getAccessToken(): Promise<string | null>;
}

/** Configuration for a Scoreboardly client instance. */
export interface ScoreboardlyOptions {
  /** API root including /api, for example https://scores.example.com/api. The SDK appends /scoreboards/{id}. */
  baseUrl: string;
  /** Public board identifier. This is not a secret or security boundary. */
  scoreboardId: string;
  /** Provider for short-lived, board-scoped bearer tokens used for score writes. */
  auth: AuthProvider;
  /** Optional fetch override for tests and runtimes without global fetch. */
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
  /** Stable category for application-level error handling. */
  public readonly code: ScoreboardlyErrorCode;
  /** HTTP status, when the error came from an HTTP response. */
  public readonly status?: number;

  /**
   * @param code Stable error category.
   * @param message Human-readable explanation.
   * @param status HTTP status when available.
   */
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
  /**
   * Retrieves the latest public leaderboard snapshot without requesting a token.
   * @returns Parsed board and valid score entries.
   * @throws ScoreboardlyError for network, HTTP, or malformed-response failures.
   */
  getLeaderboard(): Promise<Scoreboard>;
  /**
   * Submits a score using a bearer token from the configured auth provider.
   * Resolves only after a successful HTTP response. No retries, cache, or offline
   * queue are implemented in this initial client.
   * @param submission Player name and score to submit.
   * @returns Promise resolved when the API responds successfully.
   * @throws ScoreboardlyError for missing credentials, HTTP, or network failures.
   */
  submitScore(submission: ScoreSubmission): Promise<void>;
}

/**
 * Removes trailing slashes without guessing or rewriting the API path.
 * @param baseUrl Caller-supplied API root.
 * @returns Non-empty API root.
 * @throws TypeError when the root is empty.
 */
function normalizeBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim().replace(/\/+$/, "");
  if (!trimmed) {
    throw new TypeError("Scoreboardly baseUrl is required.");
  }

  return trimmed;
}

/**
 * Supports ordinary JSON and the legacy JSON-string envelope emitted by the
 * current .NET endpoint.
 * @param text Raw response body.
 * @returns Parsed payload.
 * @throws ScoreboardlyError when the body is not valid JSON.
 */
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

/**
 * Validates required board fields and filters malformed individual entries.
 * @param payload Parsed API response.
 * @returns Typed board snapshot.
 * @throws ScoreboardlyError when required fields are absent or invalid.
 */
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

/**
 * Maps common auth and qualification statuses to stable SDK error codes.
 * @param status HTTP status returned by the API.
 * @returns A typed SDK error.
 */
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
 * This initial slice provides direct reads and writes only. It does not yet
 * implement token refresh, caching, retries, idempotency, or offline queuing.
 *
 * @param options API root, board identifier, auth provider, and optional fetch.
 * @returns A client bound to the configured board.
 * @throws TypeError when required configuration is missing or invalid.
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

  /**
   * Sends a request to the configured board and normalizes transport failures.
   * @param path Optional suffix following the board URL.
   * @param init Fetch request options.
   * @returns Successful fetch response.
   * @throws ScoreboardlyError for HTTP and network failures.
   */
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
