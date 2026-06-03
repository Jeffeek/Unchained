---
name: Bug report
about: Create a report to help us improve Unchained
title: '[BUG] '
labels: 'bug'
assignees: ''
---

## Describe the bug

A clear and concise description of what the bug is.

## To Reproduce

Steps to reproduce the behaviour:

1. Load PDF with `...`
2. Call `...`
3. See error

**Minimal code snippet:**

```csharp
var processor = new DocumentProcessor();
await using var doc = await processor.LoadAsync("file.pdf");
// ...
```

## Expected behaviour

A clear and concise description of what you expected to happen.

## Actual behaviour

What actually happened. Include the full exception message and stack trace if applicable.

## Environment

| Field | Value |
|---|---|
| Unchained.Pdf version | |
| .NET version | |
| OS | |
| PDF producer (if known) | |

## Additional context

Add any other context or screenshots about the problem here. Attaching the PDF (or a minimal reproduction) is very helpful.
