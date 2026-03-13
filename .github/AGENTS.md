# KONQVIST — Agent Instructions

This is a greenfield Blazor WASM + ASP.NET Core project being built slice by slice 
following a structured build plan. 

## Reference documents
- Full PRD: /docs/PRD.md
- Build plan with milestones and slices: /docs/BUILD_PLAN.md
- These documents are the authoritative source of truth. When in doubt, check them.

## How this project is built
- One slice at a time, in the order defined in BUILD_PLAN.md
- Each slice is self-contained and ends at a verifiable state
- Do not implement ahead of the current slice

## Code style
- C# with nullable reference types enabled
- File-scoped namespaces
- Primary constructors where appropriate (.NET 10)
- No regions

## Solution design best practices (.NET 10)
- Keep classes small and focused; prefer one responsibility per class
- Keep feature code inside its vertical slice folder; avoid cross-slice coupling
- Prefer composition over inheritance for shared behavior
- Centralize shared abstractions only when reused by multiple slices
- Keep repository root engineering baselines present and maintained: `.gitignore`, `.gitattributes`, `.editorconfig`

## Testing
- Unit test project: tests/Konqvist.Server.Tests
- Run tests with: dotnet test
- Always run tests after implementing server-side logic
