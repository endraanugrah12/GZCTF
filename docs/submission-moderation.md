# Deleting flag submissions

As a platform administrator, open **Game → Monitor → Submissions**, click the
red trash icon beside an attempt, and confirm **Delete submission**. Monitor-only
accounts cannot delete attempts. Attempts still being judged cannot be deleted;
wait for the verdict first.

Deleted attempts disappear from normal submission lists, exports and recent
attack-feed queries. This is an audit-preserving deletion: the original database
record and its solver/LLM evidence remain stored. Evidence remains attached to
its attempt and cannot be reused for another flag submission. There is no UI undo.
The audit log records the acting administrator, game and submission IDs, not the flag.

If the attempt supplied the team's recorded solve, the next remaining accepted
attempt becomes its solve. Older attempts from before that recorded solve are
not resurrected (important after an administrator resets game solves). Without
a replacement, the team loses the solve and can submit again. Deleting a wrong
answer or a non-scoring duplicate does not remove another attempt's solve.

Live and frozen scoreboards are rebuilt using the normal scoring rules. This
updates solve counts, challenge values, blood bonuses, scores, ranks and timelines.
The frozen board keeps its freeze cutoff; deleting a pre-freeze solve cannot
reveal a post-freeze replacement. Cached views may take a short time to refresh.
Historical notices and already-sent Discord messages are not retracted.

This action handles normal challenge flag submissions, not A&D attack captures
or KotH control events, which use separate scoring records.

Deployment includes migration **20260922010000_SubmissionDeletion**, adding a
nullable tombstone timestamp. Back up the database before upgrading and rebuild
the application image. Do not roll back this column to undo individual deletions:
that would reveal deleted attempts without restoring their removed solve records.
