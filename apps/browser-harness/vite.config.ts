import { defineConfig, type Plugin } from "vite";
import type { IncomingMessage, ServerResponse } from "node:http";

type AuthMode = "valid" | "missing" | "expired" | "wrong-board" | "insufficient-scope";
type SubmitMode = "accept" | "conflict" | "server-error";

interface Scenario {
  online: boolean;
  latencyMs: number;
  authMode: AuthMode;
  submitMode: SubmitMode;
}

interface Entry {
  playerName: string;
  score: number;
}

const initialScenario: Scenario = {
  online: true,
  latencyMs: 0,
  authMode: "valid",
  submitMode: "accept"
};

/**
 * Adds deterministic in-memory API endpoints to the Vite development server.
 * State is process-local and reset by the harness or server restart. This
 * plugin is development-only and is not part of the production service.
 */
function mockScoreboardApi(): Plugin {
  let scenario: Scenario = { ...initialScenario };
  let scores: Entry[] = [
    { playerName: "Yeti Hunter", score: 9200 },
    { playerName: "Powder Hound", score: 8100 },
    { playerName: "Avalanche", score: 6750 }
  ];

  /** Writes JSON with no-store caching so scenario changes are immediately visible. */
  const sendJson = (response: ServerResponse, status: number, payload: unknown): void => {
    response.statusCode = status;
    response.setHeader("Content-Type", "application/json; charset=utf-8");
    response.setHeader("Cache-Control", "no-store");
    response.end(JSON.stringify(payload));
  };

  /** Reads and parses a JSON request body for the local control and score endpoints. */
  const readBody = async (request: IncomingMessage): Promise<unknown> => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) {
      chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    }
    const raw = Buffer.concat(chunks).toString("utf8");
    return raw.length > 0 ? JSON.parse(raw) as unknown : {};
  };

  /** Applies the configured delay to simulated API and token operations. */
  const pause = async (): Promise<void> => {
    if (scenario.latencyMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, scenario.latencyMs));
    }
  };

  return {
    name: "scoreboardly-mock-api",
    configureServer(server) {
      server.middlewares.use(async (request, response, next) => {
        const url = new URL(request.url ?? "/", "http://localhost");
        if (!url.pathname.startsWith("/__mock/")) {
          next();
          return;
        }

        try {
          if (url.pathname === "/__mock/reset" && request.method === "POST") {
            scenario = { ...initialScenario };
            scores = [
              { playerName: "Yeti Hunter", score: 9200 },
              { playerName: "Powder Hound", score: 8100 },
              { playerName: "Avalanche", score: 6750 }
            ];
            sendJson(response, 200, { scenario });
            return;
          }

          if (url.pathname === "/__mock/control" && request.method === "POST") {
            const update = await readBody(request) as Partial<Scenario>;
            if (typeof update.online === "boolean") scenario.online = update.online;
            if (typeof update.latencyMs === "number") {
              scenario.latencyMs = Math.min(2500, Math.max(0, Math.floor(update.latencyMs)));
            }
            if (["valid", "missing", "expired", "wrong-board", "insufficient-scope"].includes(String(update.authMode))) {
              scenario.authMode = update.authMode as AuthMode;
            }
            if (["accept", "conflict", "server-error"].includes(String(update.submitMode))) {
              scenario.submitMode = update.submitMode as SubmitMode;
            }
            sendJson(response, 200, { scenario });
            return;
          }

          if (url.pathname === "/__mock/token" && request.method === "POST") {
            await pause();
            if (!scenario.online) {
              sendJson(response, 503, { error: "Mock API unavailable" });
              return;
            }

            const tokens: Record<AuthMode, string | null> = {
              valid: "mock-token:harness-board:scores-submit",
              missing: null,
              expired: "mock-token:expired",
              "wrong-board": "mock-token:other-board:scores-submit",
              "insufficient-scope": "mock-token:harness-board:leaderboard-read"
            };
            sendJson(response, 200, {
              accessToken: tokens[scenario.authMode],
              expiresIn: 600,
              tokenType: "Bearer"
            });
            return;
          }

          const boardMatch = url.pathname.match(new RegExp("^/__mock/api/scoreboards/([^/]+)$"));
          if (boardMatch && request.method === "GET") {
            await pause();
            if (!scenario.online) {
              sendJson(response, 503, { error: "Mock API unavailable" });
              return;
            }

            sendJson(response, 200, {
              id: decodeURIComponent(boardMatch[1]),
              name: "SDK Harness Board",
              maximumScores: 10,
              scores
            });
            return;
          }

          if (boardMatch && request.method === "PUT") {
            await pause();
            if (!scenario.online) {
              sendJson(response, 503, { error: "Mock API unavailable" });
              return;
            }

            const authorization = request.headers.authorization;
            const token = typeof authorization === "string" && authorization.startsWith("Bearer ")
              ? authorization.slice("Bearer ".length)
              : "";
            if (token === "mock-token:expired" || !token) {
              sendJson(response, 401, { error: "Missing, invalid, or expired token" });
              return;
            }
            if (token === "mock-token:other-board:scores-submit" || token === "mock-token:harness-board:leaderboard-read") {
              sendJson(response, 403, { error: "Token is not authorized for this board or operation" });
              return;
            }
            if (token !== "mock-token:harness-board:scores-submit") {
              sendJson(response, 401, { error: "Unknown mock token" });
              return;
            }

            if (scenario.submitMode === "conflict") {
              sendJson(response, 409, { error: "Score did not qualify" });
              return;
            }
            if (scenario.submitMode === "server-error") {
              sendJson(response, 500, { error: "Simulated service failure" });
              return;
            }

            const body = await readBody(request) as Partial<Entry>;
            if (typeof body.playerName !== "string" || typeof body.score !== "number" || !Number.isFinite(body.score)) {
              sendJson(response, 400, { error: "Invalid score payload" });
              return;
            }
            scores = [...scores, { playerName: body.playerName.slice(0, 20), score: Math.max(0, Math.floor(body.score)) }]
              .sort((left, right) => right.score - left.score)
              .slice(0, 10);
            sendJson(response, 200, { accepted: true });
            return;
          }

          sendJson(response, 404, { error: "Unknown mock endpoint" });
        } catch (error) {
          sendJson(response, 500, {
            error: error instanceof Error ? error.message : "Mock API error"
          });
        }
      });
    }
  };
}

export default defineConfig({
  plugins: [mockScoreboardApi()],
  server: { strictPort: true, port: 5173 },
  preview: { strictPort: true, port: 4173 }
});
