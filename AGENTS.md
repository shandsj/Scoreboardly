# Repository instructions

These instructions apply to every change in this repository.

## Documentation standards

- Add documentation comments to every public interface and its contract, including each property where its meaning is not self-evident.
- Add documentation comments to every exported or public constant, class, member, and method. Document parameters, return values, errors, side effects, lifecycle expectations, and constraints when relevant to correct use.
- Document private methods and helpers when they perform meaningful work, coordinate lifecycle, enforce invariants, handle compatibility behavior, or have other non-obvious behavior. Do not comment private implementation details when the code is already clear.
- Keep documentation concise, accurate, and useful to someone maintaining or consuming the API. Explain intent and behavior, not obvious syntax.
- Update documentation whenever behavior changes. Do not leave comments that describe an old implementation.
- Prefer language-native documentation comments, such as TypeScript TSDoc/JSDoc and C# XML documentation comments, so editor tooling can surface API contracts.
- Keep examples copyable and identify whether endpoints, credentials, and configuration are provided by this repository or by the consuming game/application.

## Comment and code cleanliness

- Keep useful author notes, design rationale, and comments that explain non-obvious behavior.
- Remove boilerplate comments inherited from templates when they no longer provide useful project-specific information.
- Remove obsolete commented-out code, abandoned experiments, and stale examples when they are no longer useful. Do not preserve dead code solely for historical reference.
- Do not remove useful TODOs or author notes without resolving the underlying work or explaining why the note is obsolete.
- Add inline comments to explain why a decision exists, not to narrate an obvious line of code.
- Preserve the existing author's style and formatting where practical. Do not remove useful comments as incidental cleanup.

## Change discipline

- Keep commits and pull requests focused and cohesive. Avoid mixing unrelated cleanup into feature changes.
- Review nearby documentation whenever modifying a source file and improve it where that would help the next maintainer or consumer.
- Include documentation updates with the code they explain, and mention notable documentation or cleanup changes in the pull request summary.
- Keep documentation consistent with the implementation. Clearly distinguish implemented behavior, limitations, and planned work.
- Add or update tests when changing documented behavior or public contracts.

## Security and compatibility

- Treat browser code and client-provided data as untrusted. Never put permanent service credentials, signing keys, or privileged secrets in browser bundles, examples, or test fixtures intended for production use.
- Document trust boundaries and identify which responsibilities belong to the SDK, the Scoreboardly service, and the consuming game's trusted backend.
- Do not describe client-side checks as security enforcement. Server-side validation remains authoritative.
- Preserve documented wire-format and legacy compatibility behavior unless a deliberate, tested change is made.
- Keep mock and test environments clearly separated from production services and credentials.
