import { describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../../../tests/setup";
import {
  createScoreboardly,
  ScoreboardlyError,
  type AuthProvider
} from "../src/index";

const API_URL = "https://scoreboardly.test/api";
const BOARD_ID = "test-board";
const expectedBoard = {
  id: BOARD_ID,
  name: "Test Board",
  maximumScores: 3,
  scores: [{ playerName: "Ada", score: 900 }]
};

function createClient(auth: AuthProvider = {
  getAccessToken: async () => "test-scoped-token"
}) {
  return createScoreboardly({
    baseUrl: API_URL,
    scoreboardId: BOARD_ID,
    auth
  });
}

describe("Scoreboardly SDK foundation", () => {
  it("reads a public leaderboard through the configured API", async () => {
    const client = createClient();

    await expect(client.getLeaderboard()).resolves.toEqual(expectedBoard);
  });

  it("supports the legacy JSON-string envelope returned by the current .NET API", async () => {
    server.use(
      http.get(`${API_URL}/scoreboards/:boardId`, () =>
        new HttpResponse(JSON.stringify(JSON.stringify(expectedBoard)), {
          status: 200,
          headers: { "Content-Type": "application/json" }
        })
      )
    );

    await expect(createClient().getLeaderboard()).resolves.toEqual(expectedBoard);
  });

  it("attaches the short-lived bearer token to score submissions", async () => {
    let authorization: string | null = null;

    server.use(
      http.put(`${API_URL}/scoreboards/:boardId`, async ({ request }) => {
        authorization = request.headers.get("authorization");
        return new HttpResponse(null, { status: 200 });
      })
    );

    await createClient().submitScore({ playerName: "Grace", score: 1200 });

    expect(authorization).toBe("Bearer test-scoped-token");
  });

  it("maps a qualification conflict to a stable error code", async () => {
    server.use(
      http.put(`${API_URL}/scoreboards/:boardId`, () =>
        new HttpResponse(null, { status: 409 })
      )
    );

    await expect(
      createClient().submitScore({ playerName: "Grace", score: 1 })
    ).rejects.toMatchObject({
      name: "ScoreboardlyError",
      code: "conflict",
      status: 409
    });
  });

  it("does not submit when the auth provider has no token", async () => {
    let requestCount = 0;
    server.use(
      http.put(`${API_URL}/scoreboards/:boardId`, () => {
        requestCount += 1;
        return new HttpResponse(null, { status: 200 });
      })
    );

    await expect(
      createClient({ getAccessToken: async () => null }).submitScore({
        playerName: "Grace",
        score: 1200
      })
    ).rejects.toBeInstanceOf(ScoreboardlyError);

    expect(requestCount).toBe(0);
  });

  it("maps an expired or invalid token response to unauthorized", async () => {
    server.use(
      http.put(`${API_URL}/scoreboards/:boardId`, () =>
        new HttpResponse(null, { status: 401 })
      )
    );

    await expect(
      createClient().submitScore({ playerName: "Grace", score: 1200 })
    ).rejects.toMatchObject({ code: "unauthorized", status: 401 });
  });

  it("rejects malformed leaderboard responses", async () => {
    server.use(
      http.get(`${API_URL}/scoreboards/:boardId`, () =>
        HttpResponse.json({ id: BOARD_ID, scores: "not-an-array" })
      )
    );

    await expect(createClient().getLeaderboard()).rejects.toMatchObject({
      code: "invalid_response"
    });
  });
});
