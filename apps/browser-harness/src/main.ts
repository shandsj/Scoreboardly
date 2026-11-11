import {
  createScoreboardly,
  ScoreboardlyError,
  type Scoreboard,
  type ScoreboardlyClient
} from "@scoreboardly/sdk";
import "./style.css";

const boardId = "harness-board";

async function getMockToken(): Promise<string | null> {
  const response = await fetch("/__mock/token", { method: "POST" });
  if (!response.ok) {
    throw new ScoreboardlyError(
      "http",
      `Mock token endpoint failed with HTTP ${response.status}.`,
      response.status
    );
  }

  const result = await response.json() as { accessToken?: string | null };
  return result.accessToken ?? null;
}

const client: ScoreboardlyClient = createScoreboardly({
  baseUrl: `${window.location.origin}/__mock/api`,
  scoreboardId: boardId,
  auth: { getAccessToken: getMockToken }
});

const byId = <T extends HTMLElement>(id: string): T => {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing required harness element: #${id}`);
  }
  return element as T;
};

const scoreBody = byId<HTMLTableSectionElement>("scores");
const leaderboardStatus = byId<HTMLParagraphElement>("leaderboard-status");
const submissionStatus = byId<HTMLParagraphElement>("submission-status");
const activityLog = byId<HTMLOListElement>("activity-log");
const connectionState = byId<HTMLSpanElement>("connection-state");
const latency = byId<HTMLInputElement>("latency");
const latencyValue = byId<HTMLOutputElement>("latency-value");

function addActivity(message: string, tone: "normal" | "success" | "error" = "normal"): void {
  const empty = activityLog.querySelector(".log-empty");
  empty?.remove();

  const item = document.createElement("li");
  item.className = `log-entry log-${tone}`;
  const time = document.createElement("time");
  time.textContent = new Date().toLocaleTimeString();
  const text = document.createElement("span");
  text.textContent = message;
  item.append(time, text);
  activityLog.prepend(item);

  while (activityLog.children.length > 25) {
    activityLog.lastElementChild?.remove();
  }
}

function renderScores(board: Scoreboard): void {
  scoreBody.replaceChildren();
  if (board.scores.length === 0) {
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.colSpan = 3;
    cell.className = "empty";
    cell.textContent = "No scores on this board yet.";
    row.append(cell);
    scoreBody.append(row);
    return;
  }

  board.scores.forEach((entry, index) => {
    const row = document.createElement("tr");
    const rank = document.createElement("td");
    const name = document.createElement("td");
    const score = document.createElement("td");
    rank.className = "rank";
    score.className = "score";
    rank.textContent = String(index + 1).padStart(2, "0");
    name.textContent = entry.playerName;
    score.textContent = entry.score.toLocaleString();
    row.append(rank, name, score);
    scoreBody.append(row);
  });
}

function describeError(error: unknown): string {
  if (error instanceof ScoreboardlyError) {
    return `${error.code}: ${error.message}`;
  }
  return error instanceof Error ? error.message : "An unexpected error occurred.";
}

async function refreshLeaderboard(): Promise<void> {
  leaderboardStatus.textContent = "Fetching leaderboard through the SDK…";
  try {
    const board = await client.getLeaderboard();
    renderScores(board);
    leaderboardStatus.textContent = `Received ${board.scores.length} entries from the mock API.`;
    connectionState.textContent = "API available";
    connectionState.className = "status status-online";
    addActivity("GET leaderboard · 200 OK", "success");
  } catch (error) {
    leaderboardStatus.textContent = describeError(error);
    connectionState.textContent = "API unavailable";
    connectionState.className = "status status-offline";
    addActivity(`GET leaderboard · ${describeError(error)}`, "error");
  }
}

async function updateScenario(): Promise<void> {
  const online = byId<HTMLSelectElement>("online").value === "online";
  const authMode = byId<HTMLSelectElement>("auth-mode").value;
  const submitMode = byId<HTMLSelectElement>("submit-mode").value;
  const latencyMs = Number(latency.value);
  latencyValue.textContent = `${latencyMs} ms`;

  const response = await fetch("/__mock/control", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ online, latencyMs, authMode, submitMode })
  });

  if (!response.ok) {
    addActivity("Could not update mock scenario.", "error");
    return;
  }

  connectionState.textContent = online ? "API available" : "API unavailable";
  connectionState.className = online ? "status status-online" : "status status-offline";
  addActivity(`Scenario updated · ${online ? "online" : "unavailable"} · ${authMode} token · ${submitMode}`);
}

byId<HTMLButtonElement>("refresh").addEventListener("click", () => {
  void refreshLeaderboard();
});

byId<HTMLFormElement>("score-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const playerName = byId<HTMLInputElement>("player-name").value.trim();
  const score = Number(byId<HTMLInputElement>("score").value);

  if (!playerName || !Number.isSafeInteger(score) || score < 0) {
    submissionStatus.textContent = "Enter a player name and a non-negative whole-number score.";
    return;
  }

  submissionStatus.textContent = "Requesting a scoped token and submitting score…";
  try {
    await client.submitScore({ playerName, score });
    submissionStatus.textContent = "Score accepted by the mock API.";
    addActivity(`PUT score · 200 OK · ${playerName}: ${score.toLocaleString()}`, "success");
    await refreshLeaderboard();
  } catch (error) {
    const message = describeError(error);
    submissionStatus.textContent = message;
    addActivity(`PUT score · ${message}`, "error");
  }
});

for (const id of ["online", "auth-mode", "submit-mode"]) {
  byId<HTMLSelectElement>(id).addEventListener("change", () => {
    void updateScenario();
  });
}

latency.addEventListener("input", () => {
  latencyValue.textContent = `${latency.value} ms`;
});
latency.addEventListener("change", () => {
  void updateScenario();
});

byId<HTMLButtonElement>("reset").addEventListener("click", async () => {
  await fetch("/__mock/reset", { method: "POST" });
  byId<HTMLSelectElement>("online").value = "online";
  byId<HTMLSelectElement>("auth-mode").value = "valid";
  byId<HTMLSelectElement>("submit-mode").value = "accept";
  latency.value = "0";
  latencyValue.textContent = "0 ms";
  submissionStatus.textContent = "Scenario reset. No score submitted yet.";
  addActivity("Mock API reset.", "success");
  await refreshLeaderboard();
});

void updateScenario().then(refreshLeaderboard);
