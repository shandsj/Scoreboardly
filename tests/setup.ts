import { afterAll, afterEach, beforeAll } from "vitest";
import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";

const apiUrl = "https://scoreboardly.test/api";

export const server = setupServer(
  http.get(`${apiUrl}/scoreboards/:boardId`, ({ params }) =>
    HttpResponse.json({
      id: String(params.boardId),
      name: "Test Board",
      maximumScores: 3,
      scores: [{ playerName: "Ada", score: 900 }]
    })
  ),
  http.put(`${apiUrl}/scoreboards/:boardId`, ({ request }) => {
    if (request.headers.get("authorization") !== "Bearer test-scoped-token") {
      return new HttpResponse(null, { status: 401 });
    }

    return new HttpResponse(null, { status: 200 });
  })
);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
