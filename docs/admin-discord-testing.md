# Discord messages and administrator testing

## Discord configuration

Open **Admin → Games → select a game → Info**. Save the game's Discord webhook URL
using the game Save button. The **Discord notifications and message editor** below
it has its own **Save Discord settings** button.

Each game has independent delivery switches (bloods, announcements, hints, new
challenges and cheat alerts), bot display name/avatar, an anonymous team label,
blood titles, message templates and an embed footer. Blank message templates disable
that message type. The preview uses sample values and does not send a real message.

Open **Webhook destinations** to set separate HTTPS webhook URLs for first blood,
second blood, third blood, new challenges, announcements, hints and cheat alerts.
For example, route bloods to a public celebration channel and cheat alerts to a
staff channel. Save these with **Save Discord settings**. Blank destinations use
the game's default webhook; if neither is set, that type is not sent. Dedicated
destinations work without a default webhook. Each event is delivered once, to its
selected destination, and all delivery toggles and freeze privacy rules still apply.
You can reuse the same URL across several types. No database migration is needed.

Supported placeholders:

| Message | Placeholders |
| --- | --- |
| Blood title/body | `{team}`, `{challenge}`, `{game}` |
| Announcement | `{message}`, `{game}` |
| Hint/new challenge | `{challenge}`, `{game}` |
| Cheat alert | `{team}`, `{details}`, `{game}` |
| Footer | `{game}` |

At the exact scoreboard freeze time, automated team names become the anonymous
label. Cheat details are withheld because they can identify other teams. This also
applies to delayed deliveries and continues after game end until the freeze setting
is cleared. Discord blood delivery continues even when the public player feed
withholds blood notices. Existing Discord messages are not retroactively edited.
Do not manually put real team identities in templates or announcements: manually
written text is not an identity-redaction service. Mentions are disabled and
interpolated player text is Markdown-escaped.

This uses Discord webhooks, not a separately hosted Discord bot or slash commands.
Settings are stored in the existing Configs table; no new database migration is
required for these options. Existing webhook URLs and default event types continue
to work without configuring templates.

## Test before the start

1. Log in using a global **Admin** account. Hidden games appear in the game list.
2. Create a dedicated testing team, join the game, and accept its participation
   under **Admin → Games → Review** if approval is required.
3. Enable/approve the challenges you want to test. Division permissions, evidence
   requirements and container limits still apply.
4. Open the player's game overview. In **Admin preview and testing**, click
   **Test challenges**. The real game start time does not need to change.
5. Submit flags and evidence normally. Pre-start checks store their answer status
   and evidence, but do not create first solves, award bloods, count toward live
   submission limits or emit public solve/Discord notifications. Inspect their
   records in the existing monitor submissions/evidence pages.

Ordinary players still cannot access challenges before the scheduled start.
This preview covers the ordinary challenge/flag/container flow; it does not advance
scheduled A&D checker rounds or bypass A&D engine restrictions. Tests start real
containers and use real resources. Stop test containers when finished.

Direct TCP container endpoints are displayed and copied as `IP:port`, without an `nc` command.
IPv6 addresses retain brackets, such as `[::1]:5000`. Configured HTTPS and WebSocket
proxy URLs are unchanged.

Set `ContainerProvider.PublicIP` in `compose/appsettings.json` to the actual
challenge host IP. For your deployment, use `38.147.122.175`. Keep the website's
domain unchanged. Restart GZCTF after changing provider settings and recreate
existing instances: previously stored container addresses are not rewritten.
Fresh Compose wizard runs ask for `CHALLENGE_PUBLIC_IP`. Older configurations
without `PublicIP` resolve `PublicEntry` to an IP for compatibility, but this is
not reliable behind a CDN: always set the real origin IP explicitly. With multiple
challenge hosts, the advertised IP must actually forward the assigned port to the
host running the container.

## Docker runtime logs

Open **Admin → Games → Challenges → edit a challenge → Container runtime logs →
View Docker logs**. Select a retained test, shared or team container, choose the
number of tail lines, and use Refresh or opt into ten-second polling. The panel
shows stdout/stderr with timestamps, the Docker state, exit code and OOM-killed
indicator. Output is capped at 64 KiB and each read has an eight-second timeout.

These are the challenge container's runtime logs (the same source as `docker logs`),
not the entire platform Compose stack's logs. Build output remains in the separate
build-log section. Deleted containers have no retained Docker logs; this feature
does not add central log archival. Only containers recorded against the selected
game/challenge are accessible, not arbitrary Docker IDs or unrelated Compose
services. Kubernetes/external self-hosted service log viewing is not implemented.
Logs can contain flags and secrets; do not share them with players.

## Hide your testing team

Use **Hide my account's teams from scoreboards (all games)** on the game overview.
This reuses the account visibility setting also available in Admin → Users.
It hides every team containing your account from both live and frozen standings,
not just the current game, and can be reversed. Hidden teams do not consume blood
slots for subsequent solves. Existing submissions, evidence and administrative
logs are retained; this is not deletion or a ban.

Use a dedicated testing team so real teammates are not hidden accidentally.
After the game starts, submissions use the normal scoring workflow, so leave the
visibility toggle enabled if you continue testing during the event.
