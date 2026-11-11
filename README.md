

## JavaScript SDK workspace

The repository includes a framework-independent TypeScript SDK workspace and a local browser harness. The harness uses the SDK package against a deterministic mock API; it does not contact the production Scoreboardly service.

### Prerequisites

- Node.js 22.12 or newer
- npm 10 or newer

### Run the tests

```bash
npm install
npm test
npm run typecheck
npm run build
```

### Run the browser harness

```bash
npm run dev:harness
```

Open the local URL printed by Vite. The scenario controls simulate API availability, latency, token failures, board/scope mismatches, and score-submission outcomes. The token exchange in this harness is entirely mocked. It does not provision credentials or issue real Scoreboardly tokens.

The workspace intentionally starts with a thin public client contract and injectable authentication provider. Production credential provisioning, signed token issuance/validation, caching, idempotency, and offline queue behavior are tracked in separate issues.
