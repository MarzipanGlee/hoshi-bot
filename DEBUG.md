# DEBUG.md

Notes for debugging a live deployment (dev/prod server), as opposed to local development —
see [README.md](README.md) for local dev and [CLAUDE.md](CLAUDE.md) for coding conventions.

## Getting logs off the dev/prod server

`HoshiBot.Host`/`HoshiBot.Web` log via Serilog to two destinations: console (stdout/stderr,
same as before) and a rolling daily file (`Serilog.Sinks.File`, 14 days retained). In
production, `compose.yaml` bind-mounts each service's `/app/logs` to a host-visible
`./logs/bot`/`./logs/web` directory (next to the repo checkout `deploy.sh` runs from) — so log
files are directly readable/copyable on the host, no `docker compose logs` needed.

**EF Core's SQL logging is turned down to Warning** (`Microsoft.EntityFrameworkCore.Database.Command`
in each app's `appsettings.json`). At Information — the default — EF writes every statement it
executes, in full, multi-line. On the bot that was ~708,000 statements a day: **260 MB per day, 3.3 GB
across the 14 retained files**, and 99.9% of every log file. Failures still log at Error, so a broken
query is as visible as it ever was.

If you need the SQL back for a debugging session, raise that one override to `Information` and
remember to lower it again — a day of it costs a quarter of a gigabyte.

There is no remote shell/file access from a Claude Code session to the dev/prod server — the
only way to get logs to Claude for debugging/confirmation is to pull them manually and paste
them into the conversation:

1. SSH into the dev/prod server, from the repo checkout `deploy.sh` normally runs from.
2. Read the relevant file(s):
   ```bash
   tail -n 200 logs/bot/bot-$(date +%Y%m%d).log    # HoshiBot.Host — the Discord bot
   tail -n 200 logs/web/web-$(date +%Y%m%d).log    # HoshiBot.Web — the admin UI
   ```
   (Files roll over at UTC midnight, named `bot-YYYYMMDD.log`/`web-YYYYMMDD.log`.)
3. Paste the output into the chat, or `scp`/copy the file itself and share/paste its
   contents.

To cut noise when confirming/debugging a specific feature, filter it:
```bash
grep -iE "error|exception|forbidden|stfcnews|clientrelease|incursion" logs/bot/bot-*.log
```

Fallback, if the bind-mounted files are ever missing/unreadable (e.g. before this change was
deployed, or a container recreated without the volume): `docker compose logs --no-color
--since 30m bot` / `web` reads the same console output from Docker's own log buffer.
