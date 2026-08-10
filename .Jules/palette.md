## $(date +%Y-%m-%d) - ARIA attributes on Bootstrap components
**Learning:** Bootstrap UI components like navbar togglers and form helper text often lack semantic associations out of the box in custom C# MVC templates.
**Action:** Always verify `aria-controls` on collapse toggles and `aria-describedby` linking inputs to their hint text.

## $(date +%Y-%m-%d) - Empty States in Data Tables
**Learning:** Empty tables without data feel broken to users and provide poor onboarding experience. When `<tbody>` is empty, users lack context on what they should do next.
**Action:** Always add an explicit empty state inside the table when `Model.Any()` is false, providing an icon and a clear call-to-action (e.g. "Click 'New' to add an item").
