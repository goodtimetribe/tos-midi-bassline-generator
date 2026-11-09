# Copilot instructions

- This repository contains a .NET 10 solution: Core provides generation and MIDI
  logic, CLI handles console interaction, and API serves stateless HTTP requests.
  Keep Core independent of the front ends.
- Preserve the API's privacy guarantees: do not persist generated MIDI or request
  data, log user input or IP addresses, or expose exception details to clients.
- Keep API validation, OpenAPI documentation, examples, and tests consistent
  when changing its contract.
- Run relevant tests with `dotnet test`; CI also builds and publishes the API
  in Release configuration.
- Deployment is managed by `.github/workflows/ci.yml`. Keep deployment
  documentation in the README aligned with workflow and operational changes.
