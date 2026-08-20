# Triage Labels

User-local skills speak in terms of five canonical triage roles. This file maps those roles to the actual label strings used in this repo's issue tracker (`Skymly/DesignPatterns`). Sibling repos use the **same** label strings.

| Role in user-local skills | Label in our tracker | Meaning |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage` | `needs-triage` | Maintainer needs to evaluate this issue |
| `needs-info` | `needs-info` | Waiting on reporter for more information |
| `ready-for-agent` | `ready-for-agent` | Fully specified, ready for an AFK agent |
| `ready-for-human` | `ready-for-human` | Requires human implementation |
| `wontfix` | `wontfix` | Will not be actioned |

### Wayfinder labels (same across siblings)

| Label | Meaning |
| ----- | ------- |
| `wayfinder:map` | Effort map (parent) |
| `wayfinder:research` | Investigation ticket |
| `wayfinder:grilling` | Decision / grill ticket |
| `wayfinder:prototype` | Spike / prototype ticket |
| `wayfinder:task` | Implementation ticket |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the corresponding label string from this table.
