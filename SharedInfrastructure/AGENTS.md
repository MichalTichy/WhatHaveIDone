# Locally copied Shared.Infrastructure

These projects are local source copies, not a submodule. Root AGENTS.md and the project-specific shared-infra skill take precedence. No upstream PR or separate permission is required for local changes; never modify the source checkout as a side effect.

Read the selected project AGENTS.md before changing its API. Retain its technical rules for registration, transactions, cancellation and compatibility. References to unvendored projects describe optional upstream integrations, not available product features. The copied subset is listed in PROVENANCE.md. Build the application solution; upstream catalog/solution generators and CI are not included or required. Update the manifest and affected guidance when the copied subset changes.

No tenancy, event sourcing or emailing feature is activated by a transitive project reference. Use single-tenanted storage with explicit authenticated owner filters. Do not use automatic installer discovery to activate optional upstream features.
