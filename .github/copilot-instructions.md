# Copilot Instructions

## General Guidelines
- Prefer using POST-based endpoints for updates and soft-deletes instead of exposing HTTP PUT and DELETE endpoints. Use endpoints like POST /{id}/update for updates and POST /{id}/delete for deletions.