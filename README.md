# drop 🎮

A self-hosted, lightweight web application for sharing short video game clips.

Built with **.NET Core 10 Minimal APIs**, **Entity Framework Core (SQLite)**, and **Vue 3 (TypeScript + SCSS)**.

---

## ✨ Features

- **Automated Ingest Folder Monitoring**: Point your gameplay recorder (OBS, Medal.tv, NVIDIA ShadowPlay, etc.) or file sync tool to the `./data/ingest` folder. As soon as a clip finishes saving, `drop` automatically detects, stabilizes, generates a thumbnail via `ffmpeg`, and imports it into your library.
- **Single Account Authentication**: Secured via container environment variables (`DROP_ADMIN_USERNAME` and `DROP_ADMIN_PASSWORD`). Only the authenticated admin can upload, edit titles, manage collections, and delete clips.
- **Publicly Viewable Everywhere**:
  - Anyone with a clip link (`/clip/:id` or `/c/:id`) can watch the video with full audio and keyboard controls without needing to log in.
  - Direct video streaming with HTTP 206 Partial Content range requests for smooth scrubbing and zero buffering.
  - Social media and Discord crawler embeds: built-in OpenGraph / Twitter Card video meta tags so links pasted in Discord, Twitter, or Slack embed directly.
- **Collections**:
  - Group clips into named highlight reels or tournament playlists (e.g., *"Apex Season 25 Highlights"*, *"Valorant Clutches"*).
  - Share public collection links (`/collection/:id`).
  - Integrated theater playlist player: automatically advances to the next clip in the collection when one finishes playing!
- **Rich Gaming-Grade Custom Video Player**:
  - Custom HTML5 player styled with dark glassmorphism.
  - Keyboard shortcuts: `Space` (Play/Pause), `F` (Fullscreen), `M` (Mute), `L` (Loop toggle), `← / →` (Seek ±5s), `↑ / ↓` (Volume ±10%).
  - Multi-speed playback (0.5x, 0.75x, 1x, 1.25x, 1.5x, 2x).
  - One-click public link copying with instant toast feedback.
- **Docker & GitHub Actions Ready**:
  - Single multi-stage container that builds both frontend and backend and includes `ffmpeg`.
  - Docker Compose file for one-command deployment.
  - GitHub Actions workflow for automatic container builds and publishing to GitHub Container Registry (GHCR).

---

## 🛠️ Tech Stack

- **Backend**: .NET Core 10 Minimal API (`net10.0`)
- **Database**: SQLite with Entity Framework Core 10 (Code First)
- **Frontend**: Vue 3 + TypeScript + SCSS + Vite + Vue Router 4 + `@lucide/vue`
- **Video Processing**: FFmpeg & FFprobe (automatic thumbnail extraction & duration detection)
- **Deployment**: Docker, Docker Compose, GitHub Actions

---

## 🚀 Quick Start with Docker

### 1. Run with Docker Compose (Local Build)

Clone the repository and run:

```bash
docker compose up -d --build
```

The application will be accessible at `http://localhost:5000`.

### 2. Production Deployment (Prebuilt Image)

To deploy on a remote server or NAS using the published image from GitHub Container Registry (without needing source code or compilers):

```bash
# 1. Download docker-compose.prod.yml
# 2. Update the image name and your secrets, then run:
docker compose -f docker-compose.prod.yml up -d
```

### 3. Configure Environment Variables

Edit `docker-compose.yml` or create an `.env` file to customize:

| Variable | Default | Description |
|---|---|---|
| `DROP_ADMIN_USERNAME` | `admin` | Username for single admin account |
| `DROP_ADMIN_PASSWORD` | `change-me-promptly` | Password for single admin account |
| `DROP_JWT_SECRET` | *(auto-generated)* | 32+ character key for JWT token signing |
| `DROP_INGEST_PATH` | `/data/ingest` | Path to folder monitored for new clips |
| `DROP_STORAGE_PATH` | `/data/clips` | Path where permanent video files are kept |
| `DROP_DB_PATH` | `/data/db/drop.db` | SQLite database file location |
| `DROP_INGEST_DEFAULT_VISIBILITY` | `public` | Default visibility for ingested clips (`public`, `unlisted`, `private`) |

---

## 📂 Ingest Folder Workflow

1. Configure your screen recording software (OBS, Medal, ShadowPlay) or copy video files directly into:
   ```
   ./data/ingest/
   ```
2. The background ingest service monitors this directory every few seconds.
3. Once file writing is finished and the process releases the file lock:
   - The file is securely moved to the clips storage directory.
   - An 8-character unique public identifier (e.g. `xK8m9zL2`) is generated.
   - A thumbnail is automatically extracted at 1 second.
   - The clip is recorded in SQLite and immediately visible on your homepage!

---

## 💻 Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (v20+)
- [FFmpeg](https://ffmpeg.org/) (optional for local thumbnail generation)

### 1. Build and Run Backend

```bash
cd backend/Drop.Api
dotnet run
```
API runs on `http://localhost:5000`.

### 2. Run Frontend Dev Server

In a second terminal:

```bash
cd frontend
npm install
npm run dev
```
Frontend runs on `http://localhost:5173` with automatic API proxying to `http://localhost:5000`.

### 3. Run Automated Tests

**Backend Tests (Unit & API Integration):**
```bash
dotnet test
```

**Frontend Automated UI Tests (Vitest & Vue Test Utils):**
```bash
cd frontend
npm test
```

**Frontend E2E Browser Tests (Playwright):**
```bash
cd frontend
npx playwright install # First time only to download headless browsers
npm run test:e2e
```

### 4. Build Full Single-Binary Package

```bash
# 1. Build frontend into Drop.Api/wwwroot
cd frontend
npm run build

# 2. Build and publish .NET 10 API
cd ../backend/Drop.Api
dotnet publish -c Release -o ../../publish
```

---

## 🔒 Security & Public Access

- **Public Routes**:
  - `GET /` — Browse library of clips and collections
  - `GET /clip/{id}` & `GET /c/{id}` — Watch individual clip
  - `GET /collection/{id}` — Watch collection playlist
  - `GET /api/clips/{id}/stream` — Video stream with byte-range support
  - `GET /api/clips/{id}/thumbnail` — Video thumbnail
- **Admin Only**:
  - `POST /api/clips/upload` — Direct browser upload
  - `PUT /api/clips/{id}` — Edit clip title/description
  - `DELETE /api/clips/{id}` — Delete clip
  - `POST/PUT/DELETE /api/collections` — Collection management
  - `POST /api/collections/{id}/clips` — Add clips to collection

---

## 📦 GitHub Actions CI/CD

A ready-to-use GitHub Actions workflow is included at `.github/workflows/docker-build.yml`.
When you push to `main` or push a version tag (`v*.*.*`), GitHub Actions builds the multi-stage Docker container and publishes the image to GitHub Packages (`ghcr.io`).

---

## 📄 License

MIT
