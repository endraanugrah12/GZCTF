# Team leader invitations

Open **Admin → Users → CSV / Invitations**.

1. Set **Invitation lifetime (days)** and click **Save lifetime**. The default is
   30 days; supported values are 1–3650 days. This affects future imports and
   regenerated invitations only, not links already issued.
2. Upload or paste a CSV containing exactly two columns (up to 500 teams):

   ```csv
   email,team_name
   leader@example.com,Team Alpha
   another@example.com,Team Beta
   ```

3. Click **Create invitations**. Duplicate emails/team names or existing accounts
   and teams reject the entire import; no partial batch is created.
4. Use **Copy link** for each row and securely send it to that leader.
   The platform does not automatically send invitation emails.
5. The leader opens the link while signed out and chooses a username and password. The email is
   fixed by the invitation. Redemption creates the account and team together,
   assigns the account as captain/member, and signs them in.

No username/password is generated during import. Until redemption the team is
a pending reservation shown in the invitations panel, not an empty team with a
fake captain. Existing users and teams are not overwritten or reassigned.

## Managing invitations

- **Revoke** immediately disables an unused link.
- **Regenerate** replaces the token and starts a new expiration period using the
  saved lifetime. It works for pending, expired and revoked invitations.
- Redeemed invitations cannot be reissued. Normal account recovery applies.
- Email and team reservations remain after revocation/expiry; use the existing
  row's Regenerate action, rather than importing it again.
- Changing the default lifetime does not extend already-issued links.

## Security and deployment

Links are bearer credentials: anyone with a link can claim its bound email/team.
Give each link only to its intended leader. An admin invitation explicitly
approves that email/account, including when public registration is disabled.
Configured CAPTCHA, fingerprint checks and Identity password validation still
apply. Invitations always create ordinary users, never administrators.

Tokens contain 256 random bits. Lookup uses a SHA-256 hash; the recoverable copy
used by the admin Copy link action is protected with ASP.NET Data Protection.
Keep the database's Data Protection keys alongside database backups. Links put
tokens in URL fragments, not query strings, and public APIs receive tokens in
POST bodies. Do not enable request-body logging on these endpoints.

Redemption is single-use, with a database transaction and concurrency version
covering token consumption, account creation and captain/member assignment.
Failed account validation rolls back the token claim.

Deployment includes migration **20260916010000_TeamLeaderInvitations**, adding one
table and indexes. Back up the database before upgrading. No existing accounts,
credentials or teams are modified. The old credential-import API is retained
for compatibility, but the Admin Users CSV interface uses invitations only.

The isolated PostgreSQL regression tests can be enabled with
`GZCTF_INVITATION_TEST_CONNECTION`. They create and drop uniquely named
`invitation_test_*` databases; never point this setting at a production server.
