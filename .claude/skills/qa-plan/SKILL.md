# qa-plan

Generates a structured QA plan as a GitHub issue by analysing recent git commits and the files they changed. Use when the user wants to produce or refresh a QA checklist after finishing a feature branch or a batch of work on main.

## Project context

- .NET / Docker app — ASP.NET Core API with a single-page HTML frontend
- Background computation process: Macaulay2 (M2)
- App starts with `docker compose up --build` and is reachable at `http://localhost:5000`
- Test suite: `dotnet test`
- GitHub CLI path on Windows: `"C:/Program Files/GitHub CLI/gh.exe"`

## Arguments

`/qa-plan [scope]` — scope identifies which commits to cover (e.g. `8`, `last 3`, `the csv and analysis slices`). **Required on main/master — if not provided, ask the user which commits to include before proceeding.** Optional on feature branches — if omitted, all branch-only commits are used automatically; if provided, use only the matching subset.

## Steps

1. **Get branch name**
   ```
   git branch --show-current
   ```

2. **Determine commit range**
   - On `main` or `master`: if no scope was provided, stop and ask the user "Which commits should the QA plan cover?" before continuing. The user may give a number (e.g. `8`) or a description (e.g. `the csv and analysis slices`) — use `git log --oneline` to identify the matching commits, then confirm with the user before proceeding.
   - On any other branch: if scope was provided, use `git log --oneline main..HEAD` to list branch commits then identify the matching subset; otherwise use all of `git log --oneline main..HEAD`

3. **Read file stats for that range**
   - On main/master: `git log --stat` for the identified commits
   - On feature branch: `git log --stat main..HEAD`

4. **Detect the parent issue (feature branch only)**

   If the branch name contains an issue number (e.g. `issue/15-...` or `feature/42-...`), extract it as `{parent-issue-number}`. Use the GraphQL API to get its node ID — you'll need it for the sub-issue link in step 7.

   ```
   "C:/Program Files/GitHub CLI/gh.exe" api graphql -f query='
   query {
     repository(owner: "{owner}", name: "{repo}") {
       issue(number: {parent-issue-number}) { id title }
     }
   }'
   ```

   If no issue number is found in the branch name, skip sub-issue linking.

   Then check for an existing QA plan issue:
   ```
   "C:/Program Files/GitHub CLI/gh.exe" issue list --search "QA Plan: {branch-name}" --json number,title
   ```
   Skip this entire step on main/master.

5. **Detect M2 changes**

   Scan the changed file paths from step 3. If any path starts with `m2/` (e.g. `m2/ext-shifting/lib/*.m2`, `m2/ext-shifting/libs.m2`):
   - Read each changed `.m2` file
   - Identify all public functions defined at the top level (lines matching `^functionName = ...`) and their `doc ///` examples if present
   - You will use this to generate an `## M2 Unit Tests` section in step 6

6. **Generate the QA plan body** (write to a temp file, e.g. `/tmp/qa-plan-body.md`)

   Structure:
   ```
   ## Prerequisites
   - [ ] `docker compose up --build`
   - [ ] Open browser at http://localhost:5000

   ## M2 Unit Tests                          ← include this section only if M2 files changed (step 5)
   Run these in a Macaulay2 terminal (`M2`) from the `m2/ext-shifting/` directory.

   ### Load library
   - [ ] Load the full library with no errors:
         ```
         load "libs.m2"
         ```
     - Expected: no error output; M2 prompt returns normally

   ### <function or change name>
   - [ ] <concrete M2 expression — use the doc /// example if one exists, otherwise construct a
         minimal input that exercises the function>
         ```
         <M2 expression>
         ```
     - Expected: <exact M2 output> — derive this from the function's logic and the input; be specific

   (one subsection per public function touched in the diff; group by commit if helpful)

   ## Test Steps (organised by commit / slice)
   (one section per commit or logical slice, with checkbox items)

   ### <commit subject or slice name>
   Changed files: <list>
   - [ ] <UI interaction or endpoint to verify>
     - Expected: <what the UI should show or do, inferred from the diff; fall back to a behavioural description if the diff is ambiguous>
   - [ ] <description of what to test>:
         ```
         curl -s -o response.json -w "%{http_code}" <METHOD> http://localhost:5000/<path> -H "Content-Type: application/json" -d "{\"key\":\"value\"}"
         ```
         `echo. && jq . response.json`
     - Expected: <HTTP status code and key fields in the response body, inferred from the diff>
   - [ ] <edge case>
     - Expected: <what should happen, inferred from the diff>

   ## Regression Checks
   - [ ] `dotnet test` passes with no failures
   - [ ] No errors in browser console
   - [ ] Restart container (`docker compose down && docker compose up --build`) and retest happy path
   ```

   **M2 test generation rules:**
   - Every M2 test expression must be runnable verbatim in an M2 terminal — no placeholders
   - Derive expected output from the function's logic and the concrete input; never write "some output" or leave it vague
   - For renamed functions, include a test that calls the new name (not the old one)
   - For new typed HashTables (e.g. `new FooType from {...}`), include a field-access test (`obj#field`) to confirm named access works
   - One `load "libs.m2"` at the top of the section — do not repeat it per function
   - Keep expressions short: prefer literals like `{0,1,2}` over constructed variables where possible

7. **Ensure the `qa-plan` label exists**
   ```
   "C:/Program Files/GitHub CLI/gh.exe" label create qa-plan --color 0075ca --description "QA plan issue" --repo <owner/repo> 2>/dev/null || true
   ```

8. **Create or update the GitHub issue**

   Issue title:
   - Feature branch with parent issue: `QA Plan: {branch-name} (#{parent-issue-number})`
   - Feature branch without parent issue: `QA Plan: {branch-name}`
   - Main/master: `QA Plan: last {N} commits`

   - If an existing issue was found (step 4):
     ```
     "C:/Program Files/GitHub CLI/gh.exe" issue edit {number} --title "{title}" --body-file /tmp/qa-plan-body.md --add-label qa-plan
     ```
   - Otherwise:
     ```
     "C:/Program Files/GitHub CLI/gh.exe" issue create --title "{title}" --body-file /tmp/qa-plan-body.md --label qa-plan
     ```

   After creating/updating, if a parent issue was found in step 4, link the QA plan as a sub-issue using the node IDs:
   ```
   "C:/Program Files/GitHub CLI/gh.exe" api graphql -f query='
   mutation {
     addSubIssue(input: { issueId: "{parent-node-id}", subIssueId: "{qa-plan-node-id}" }) {
       issue { number }
       subIssue { number }
     }
   }'
   ```
   Get the QA plan issue's node ID with:
   ```
   "C:/Program Files/GitHub CLI/gh.exe" api graphql -f query='
   query {
     repository(owner: "{owner}", name: "{repo}") {
       issue(number: {qa-plan-issue-number}) { id }
     }
   }'
   ```

9. **Report** the issue URL to the user.

## Rules

- Every testable action must be a GitHub checkbox: `- [ ]`
- Every checkbox must have an indented `  - Expected: <result>` line beneath it — Claude generates this from the diff; be as specific as the diff allows, fall back to a behavioural description if the diff is ambiguous
- All API test steps must follow the two-line pattern: `curl ... -o response.json -w "%{http_code}"` then `echo "" && jq . response.json`, with an `Expected:` line stating the HTTP status and key response fields
- curl commands go inside a fenced code block; the `echo. && jq . response.json` follow-up goes on its own line as inline code
- curl JSON bodies use Windows CMD syntax: `-d "{\"key\":\"value\"}"` (double-quoted string, inner quotes backslash-escaped) — never single quotes. also, CMD doesn't allow backslash line continuations.
- All M2 test expressions must be runnable verbatim; expected output must be exact (not vague)
- Regression Checks and Prerequisites do NOT get `Expected:` lines — they are self-explanatory
- Cover every endpoint, UI interaction, and edge case visible in the diff
- Do not hard-code assumptions about unrelated features; derive everything from the commit stats
