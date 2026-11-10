---
name: create-github-issue-feature-from-specification
description: "Create GitHub Issue for feature request from specification file using feature_request.yml template."
argument-hint: "Path to the specification"
disable-model-invocation: true
---

## Invocation guidance

Treat `${file}` below as the specification supplied with the invocation or unambiguous active context. If no specification is identifiable, ask for its path.

# Create GitHub Issue from Specification

Create GitHub Issue for the specification at `${file}`.

## Process

1. Analyze specification file to extract requirements
2. Check existing issues using `search_issues`
3. Create new issue using `create_issue` or update existing with `update_issue`
4. Use `feature_request.yml` template (fallback to default)

## Requirements

- Single issue for the complete specification
- Clear title identifying the specification
- Include only changes required by the specification
- Verify against existing issues before creation

## Issue Content

- Title: Feature name from specification
- Description: Problem statement, proposed solution, and context
- Labels: feature, enhancement (as appropriate)