# Challenge image build modes

In Admin → Games → Challenges, click the hammer on a challenge card, or open
the challenge editor and click Build. Choose:

- **Build using cache (faster)**: reuse an existing matching image or Docker's
  cached build steps. This remains the default for automated imports.
- **Build from scratch (no cache)**: bypass the matching-image shortcut and
  rerun Dockerfile steps without layer cache (Docker or Kubernetes BuildKit).

The choice applies to this build and its automatic retries, including its
checker image when present. Build logs identify the selected mode.
For repository fallback scans, only the selected challenge gets no-cache mode;
other imported challenges retain normal caching.

No-cache does not mean pulling a newer base image, deleting all Docker caches,
or restarting existing player instances. It builds from the stored challenge
archive (or the repository fallback when no archive is available).
Stop and start an instance to use the rebuilt image.

API: `POST /api/edit/games/{id}/challenges/{challengeId}/rebuild?noCache=true`.
Omitting `noCache` preserves cached builds. Existing game-admin authorization
and duplicate-build protection apply to both modes.
