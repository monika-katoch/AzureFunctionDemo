# Local Control Panels (HTML frontends)

Two dependency-free HTML pages to exercise every function from a browser:

| Page | Drives | Default host |
|------|--------|--------------|
| [stateless.html](stateless.html) | `SimpleHttpFunction` — S1–S5 learning + Orders API | `http://localhost:7218` |
| [durable.html](durable.html) | `HelloDurable` + `DurableFunctionDemo` orchestrations | `7071` and `7219` |

Each page has a **Base URL** field, request/response panels, and (for durable)
live status polling with a colored status dot.

---

## Run it (the important part: enable CORS)

The pages are served from a different origin than the functions, so each host
**must start with CORS enabled**. Start every host with the `--cors` flag:

```bash
# Stateless app
cd SimpleHttpFunction && func start --port 7218 --cors "*"
```
```bash
# Durable apps (start Azurite first, in its own terminal)
npx azurite --silent --location ./__azurite
```
```bash
cd HelloDurable && func start --port 7071 --cors "*"
```
```bash
cd DurableFunctionDemo && func start --port 7219 --cors "*"
```

> The projects' `local.settings.json` also set `"Host": { "CORS": "*" }`, but the
> **`--cors "*"` flag is the reliable switch** — always pass it for the frontends.
> `"*"` is for local demo only; in Azure, list your real site's origin instead.

### Open the pages

Because browsers block `fetch` from `file://` pages, serve this folder over HTTP:

```bash
cd frontend && npx http-server -p 8080 -c-1
```

Then open **http://localhost:8080/stateless.html** and
**http://localhost:8080/durable.html**.

---

## What each page does

**stateless.html**
- S1–S5: one button per learning endpoint (route params, JSON body, auth levels, DI).
- Orders: set the `x-api-key` and `Idempotency-Key`, then Create / List / Get.
  Try Create twice with the *same* idempotency key → the second returns `200` (no duplicate).

**durable.html**
- *HelloDurable*: **Start** → reads the built-in `statusQueryGetUri` → polls until `Completed` → shows `["Hello, Tokyo!", …]`.
- *Customer dashboard*: **Start** for a `userId` → polls `/dashboard/status/{id}` → shows the merged parallel result. Watch the host console to see the retry policy recover the flaky Orders source.

All four flows were verified end-to-end in a browser against the running hosts.
