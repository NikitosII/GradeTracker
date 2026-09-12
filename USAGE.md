# Usage

Commands to launch GradeTracker and the links to reach it. For what the project
is and how it's built, see [README.md](README.md).

**Prerequisites:** Docker + Docker Compose v2, and a Telegram bot token from
[@BotFather](https://t.me/BotFather).

> **Run every command from the repo root**, and pass **`--project-directory .`**
> on each `docker compose` command. The compose files live in `docker/`, so
> without that flag Compose resolves the build context, the `./ops` mounts and
> `.env` against `docker/` instead of the root — the build breaks and `.env` is
> silently ignored (empty tokens). `--project-directory .` pins all of that back
> to the root.

---

## 1. Configure

```bash
cp .env.example .env
```

Then edit `.env` and fill in at least `TELEGRAM_BOT_TOKEN`,
`TELEGRAM_WEBHOOK_SECRET` and `BOOTSTRAP_ADMIN_CODE`. For production also set
`DOMAIN`, `ACME_EMAIL`, `GRAFANA_ADMIN_PASSWORD` and strong database/RabbitMQ
passwords.

---

## 2. Run locally

```bash
docker compose --project-directory . \
  -f docker/docker-compose.yml -f docker/docker-compose.override.yml up --build
```

Links:

- Bot HTTP — http://localhost:8080
- Liveness / readiness — http://localhost:8080/health/live · http://localhost:8080/health/ready
- bot-web metrics — http://localhost:9464/metrics
- bot-worker metrics — http://localhost:9465/metrics
- RabbitMQ UI — http://localhost:15672

To test webhook delivery locally, expose port 8080 with a tunnel and set
`TELEGRAM_WEBHOOK_URL` to that HTTPS URL, then restart:

```bash
ngrok http 8080
```

---

## 3. Run locally with monitoring

Adds the OpenTelemetry collector, Jaeger, Prometheus, Loki and Grafana. Set the
file list once so every `docker compose` command includes the monitoring wiring:

```powershell
$env:COMPOSE_FILE = "docker/docker-compose.yml;docker/docker-compose.override.yml;docker/docker-compose.monitoring.yml"
docker compose --project-directory . up -d --build
```

> **This is the #1 gotcha.** `$env:COMPOSE_FILE` lasts only for the current
> terminal — set it again in every new window, before the first `docker compose`
> command. Miss it and the apps come up with an empty `Observability__OtlpEndpoint`,
> export no telemetry, and **every dashboard panel reads "No data."** On Windows
> the separator is `;` (a colon `:` silently breaks it). Keep `--project-directory .`
> on the command too (see the note at the top). To fix an already-running stack,
> just re-run the command above — it recreates the apps with the collector wired in.

To confirm the apps are actually exporting:

```powershell
docker exec edutrack-bot-web-1 printenv Observability__OtlpEndpoint   # → http://otel-collector:4317
```

Links:

- Grafana — http://localhost:3000 (admin / admin) → dashboard **GradeTracker – Overview** (set the time range to **Last 15 minutes**)
- Jaeger (traces) — http://localhost:16686
- Prometheus — http://localhost:9090
- Loki API — http://localhost:3100

---

## 4. Run in production

Adds Caddy (automatic HTTPS), monitoring and scheduled backups; only Caddy
(80/443) is exposed to the internet. Both `-f` flags are required every time.

```bash
docker compose --project-directory . \
  -f docker/docker-compose.yml -f docker/docker-compose.prod.yml up -d --build
```

Links:

- Webhook — https://\<DOMAIN\>/api/telegram/webhook
- Liveness — https://\<DOMAIN\>/health/live
- Grafana — http://\<server\>:3000 (admin / `GRAFANA_ADMIN_PASSWORD`)
- Jaeger — http://localhost:16686 via `ssh -L 16686:localhost:16686 user@server`

---

## First run in the bot

There is no web sign-up — access is through Telegram, and a fresh database has
**no subjects** (they are not seeded; an admin creates them).

1. Open your bot in Telegram and send `/bind <code>`, where `<code>` is the
   `BOOTSTRAP_ADMIN_CODE` from your `.env`. This creates your account as **Admin**.
2. Send `/admin` → **Subjects** → **➕ New subject**, and add the subjects you
   want. Students can only add grades once at least one subject exists.
3. Issue invite codes for other users via `/admin` → **Invites**; they join with
   `/bind <code>`.

---

## Command reference

Most commands open an inline-button menu, so `/help` stays short and you pick the
next step from buttons rather than memorising commands.

**Student**

- `/start`, `/help` — welcome and the command list.
- `/bind <code>` — link your Telegram account with an invite code.
- `/profile` — your linked profile.
- `/grades` (alias `/subjects`) — grades by subject, with **➕ Add** / **✏️ Edit** buttons.
- `/deadlines` — your deadlines, with **Today / This week / Nearest** filters and **⚡ Quick add** / **✏️ Edit** buttons.
- `/quick <text>` — add a deadline from one line, e.g. `/quick Physics homework tomorrow 18:00`. Tolerates typos and abbreviations; if the subject isn't recognised it offers a picker.
- `/export` — download your deadlines as an `.ics` calendar file.
- `/report` — full performance report (stats and trends).
- `/tips` — personalised recommendations (overdue work, slipping subjects, this week's load…).
- `/history` — your recent grade and deadline changes.
- `/archive` — hide grades/deadlines you no longer need, or restore archived ones.
- `/settings` — notifications, morning digest, reminders, quiet hours, time zone, language.
- `/cancel` — cancel the current action.

**Admin** (in addition to the above)

- `/admin` — admin menu with buttons for **Users**, **Invites**, **Subjects**, **Audit** and **Status**.
- `/announce <text>` — broadcast a message to all users.

> Older standalone commands (`/today`, `/week`, `/next`, `/deadline_add`,
> `/deadline_edit`, `/grade_add`, `/grade_edit`, `/users`, `/invites`, `/audit`,
> `/status`) still work if typed — they're just folded into the menus above to
> keep `/help` tidy.

---

## Other commands

Follow logs:

```bash
docker compose --project-directory . -f docker/docker-compose.yml logs -f bot-web bot-worker
```

Restore a database backup:

```bash
docker compose --project-directory . -f docker/docker-compose.yml \
  exec -T postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" < dump.sql
```

Run the tests (self-contained — no Docker daemon required):

```bash
dotnet test EduTrack.sln
```
