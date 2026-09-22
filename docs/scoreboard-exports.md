# Scoreboard exports

Administrators and monitors can download **Excel (.xlsx)** or **CSV (.csv)**
from the Jeopardy scoreboard page or **Game → Monitor**.

Both files contain the full live scoreboard, not just the selected division or
the public frozen view. They include rank, team, division (when present), captain,
participant details, solved count, last solve time, total score, and scores for
each Jeopardy challenge. Hidden teams stay excluded by the scoreboard rules.
Unranked teams appear after ranked teams. Empty boards export headers normally.

These are privileged exports: participant details can include real names,
emails, student numbers and phone numbers. Share the files accordingly.
CSV files use UTF-8 with a BOM for Excel compatibility, quoted fields, and
spreadsheet-formula protection for text supplied by users.

The existing endpoint remains compatible:

- `GET /api/game/{id}/ScoreboardSheet` — Excel (default).
- `GET /api/game/{id}/ScoreboardSheet?format=csv` — CSV.
- `GET /api/game/{id}/ScoreboardSheet?format=xlsx` — Excel explicitly.

Both formats require the existing Monitor/Admin authorization. The separate
A&D/KotH export workflows are unchanged. No database migration is required.
