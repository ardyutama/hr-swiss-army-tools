# CV Sorting

This context organizes emailed job applications around a vacancy so HR can review candidates and contact them.

## Language

**Vacancy**:
A hiring effort for one role, opened on a business date and carrying an ordered set of requirements.
_Avoid_: Job, position, vacancy role

**Opening Date**:
The business date on which a vacancy's hiring effort begins, distinct from when its record is created.
_Avoid_: Vacancy date, creation date

**Vacancy Requirement**:
A distinct, ordered, matchable phrase describing something sought from candidates for one vacancy.
_Avoid_: Global skill, weighted criterion

**Vacancy Status**:
Whether a vacancy is open or closed; a new vacancy starts open.
_Avoid_: Candidate status, review status

**Open Vacancy**:
A vacancy that can be changed and can receive candidate imports.
_Avoid_: Active vacancy

**Closed Vacancy**:
A read-only vacancy retained for reference after its hiring effort ends; it can be reopened explicitly.
_Avoid_: Archived vacancy, deleted vacancy

**Purge**:
The explicit, irreversible removal of a vacancy together with all candidate information it owns.
_Avoid_: Delete, archive

**Candidate Removal**:
The explicit, irreversible removal of one candidate together with all information it owns.
_Avoid_: Delete, erase

**Intake Round**:
A named wave of application intake within one vacancy; it owns its candidate list and review workspace, and exactly one can be active per vacancy at a time. Identified by a per-vacancy round number (never reused) with an optional name.
_Avoid_: Batch interview, hiring batch

**Active Round**:
The intake round currently accepting candidate imports and edits for its vacancy; a vacancy may legally have none between waves.
_Avoid_: Current batch, open batch

**Round Closure**:
The explicit, irreversible end of an active round, marked by HR; a closed round never reopens and its review data (imports, review status, notes, requirement reviews) is read-only forever, while hire-outcome bookkeeping on its candidates stays editable until the vacancy closes (reopening is a vacancy power).
_Avoid_: Batch close, round archive

**Round Management**:
HR's administration of a vacancy's intake rounds: opening the next round and closing the active one. It changes round lifecycle only, never candidate review data.
_Avoid_: Round settings, batch management

**Settled**:
The moment a record becomes read-only through its lifecycle: a round's review data settles at round closure; hire-outcome bookkeeping settles at vacancy closure. Attempts to change settled data are refused and must be re-expressed through the lifecycle's own powers (promotion, reopening).
_Avoid_: Locked, frozen

**Lifecycle Conflict**:
A refusal of an operation because the record's lifecycle state forbids it: either an attempt to change settled data (closed round, closed vacancy) or a failed lifecycle precondition (round already active, no active round, source round not yet closed). Lifecycle conflicts are expected and actionable: they surface as amber warnings whose copy tells HR how to re-express the intent through the lifecycle's own powers. Unknown conflicts surface as red errors; unexpected faults that escape a flow's own handling are Technical Failures, never Lifecycle Conflicts.
_Avoid_: settled-state error, validation failure

**Technical Failure**:
An unexpected fault that escapes a flow's own handling — a render error, an unhandled rejection, or a failed navigation. Technical failures surface as red errors: the app shell stays alive and HR can return to the Vacancy list or reload. Never used for Lifecycle Conflicts, which are expected and amber.
_Avoid_: crash, exception, white screen

**Promote**:
Moving a new, flagged, or shortlisted-without-outcome candidate from a closed round into the active round, preserving review status, requirement reviews, and notes.
_Avoid_: Copy candidate, re-import

**Prior Application Notice**:
A display-only indicator that a candidate's identifying email (Source Sender, or the Form Response's Contact Email) appears on more than one candidate of the same vacancy — any round, including the current one; it is person-intuition, not person-proof, and never blocks import or affects review status.
_Avoid_: Duplicate block, global person history

**Candidate**:
One person's submission to one intake round, created from one intake source; the same person submitted to another round or vacancy is a different candidate.
_Avoid_: Talent, shared person, global candidate

**Intake Source**:
The channel one candidate was imported from: a Source Email (.eml upload) or a Form Response (Google Forms CSV upload). One intake round may mix both; the review workspace renders whichever evidence exists.
_Avoid_: Application channel, import type

**Source Email**:
The immutable exported email from which exactly one candidate is imported and whose original content is retained.
_Avoid_: Candidate email, live mailbox message

**Form Response**:
One row of a Google Forms CSV export from which exactly one candidate is imported; the raw row's cells are stored verbatim and never edited. Identity is the normalized response email, falling back to digits-only phone, falling back to no key (always a new candidate). Each response carries a form Timestamp column used to resolve duplicates: within one vacancy, the same email key keeps only the latest response as its Form Response, with earlier ones retained as hidden prior submissions.
_Avoid_: Spreadsheet row, form entry

**Form Layout**:
A vacancy's mapping of its Google Form's CSV columns onto the review workspace. Columns are identified by **ordinal position** (never header text), with a read-only header snapshot kept for display and Header Drift detection. Four special roles — Name, Contact Email, Contact Phone, CV Link — each bind to at most one column and pre-fill the editable Candidate Details at import; roles are always **bound manually by HR, never auto-detected**, and Name and Contact Email must be bound for a valid layout. Every other Picked Column is a Form Answer, shown display-only in column order. A vacancy picks at most 8 columns in total, roles included. The form Timestamp is never a picked column: it is system data read from ordinal 0 by Google Forms convention and stored on the Form Response. A vacancy **cannot import form responses until it has a valid layout**: the first upload routes into the guided layout panel and the import completes there; layout edits re-project over stored raw rows, pre-filling Candidate Details except where HR already typed values; re-projection never crosses a settled round, so closed-round candidates keep their details untouched.
_Avoid_: Column config, field mapping, table layout

**Header Drift**:
The detected mismatch when a newly uploaded CSV's header at a mapped ordinal differs from the Form Layout's snapshot. Import pauses behind a drift dialog listing every changed ordinal (old header → new header); HR either confirms the mapping still holds or re-maps before the import proceeds. Confirming adopts the uploaded file's header text as the new snapshot baseline (the mapping has been re-verified) and Column Labels carry over; without it the snapshot stays and every later upload pauses again. Already-imported candidates keep their stored Form Responses untouched.
_Avoid_: Schema change, column mismatch

**Column Label**:
HR's display-only short name for a form column picked in the Form Layout, bound to the column's ordinal position. The original header snapshot is always retained verbatim and shown beside the label; the label never identifies a column (ordinals do), never affects Header Drift detection, is never written by import, and is discarded if the column is un-picked. A drift confirmation re-verifies the label; labels are layout configuration, not review data, so they never Settle.
_Avoid_: Renamed column, column alias, mapped name

**Picked Column**:
A form column chosen in the Form Layout — either bound to one of the four roles or kept as a Form Answer. A vacancy picks at most 8 columns in total, roles included; the form Timestamp is never picked. Un-picking a column discards its Column Label.
_Avoid_: Mapped column, selected field

**Form Answer**:
A Picked Column bound to no role, rendered display-only on the review page in column order; it never pre-fills Candidate Details and never identifies a candidate.
_Avoid_: Display field, extra column

**Screening Rule**:
A vacancy-owned condition over one form column (`equals`, `not-equals`, `is-empty`, `not-empty`, `contains` — text only, no type parsing), AND-combined with the vacancy's other rules, evaluated against stored raw Form Responses. Rules never screen Source Email candidates (they have no form columns). Editable while the vacancy is open; re-evaluated live so reclassification is free.
_Avoid_: Knockout filter, auto-reject, validation rule

**Screened Out**:
The computed import disposition of a Form Response candidate failing at least one Screening Rule. Screened-out candidates are imported and stored, excluded from the default candidate list (with a count badge and a toggle to reveal them), never deletable, and reclassifiable only by changing rules. Screening status settles at round closure like all review data.
_Avoid_: Filtered, deleted, auto-rejected

**Screening Verdict**:
A candidate's stored screening outcome: whether it is Screened Out plus the fired rules' display descriptors. Written once at Round Closure as the candidate's screening freezes; closed-round reads render it verbatim forever (rules may later be edited or deleted without touching it), while active-round candidates carry no verdict and are evaluated live. Promoting a candidate into a new round clears the verdict so the active round re-evaluates.
_Avoid_: Screening result, cached verdict

**Resubmitted**:
The display indicator on a candidate whose stored Form Response was replaced by a newer row for the same identity key on a later upload. The review page flags the refresh; review status, notes, requirement reviews, and typed details are never altered by a re-upload.
_Avoid_: Updated, overwritten

**Source Sender**:
The sender recorded by the source email, who may differ from the candidate.
_Avoid_: Candidate contact

**Candidate Display Name**:
The display-only name for a candidate in lists: the typed name, else the source sender name, else the source sender email, else the source email subject; the stored name stays empty until HR types it.
_Avoid_: Extracted name, sender-only fallback

**CV Document**:
A PDF attachment retained from a candidate's source email; a candidate may have several CV documents.
_Avoid_: Non-PDF attachment, source email

**Primary CV** (deferred to V3):
The one CV document selected as the candidate's main document for extraction and review.
_Avoid_: First attachment

**Extraction Status** (deferred to V3):
Whether candidate details extraction is pending, succeeded, or failed, independently of manual review.
_Avoid_: Review status, import status

**Review Status**:
HR's current decision for a candidate: new, flagged, shortlisted, or rejected.
_Avoid_: Extraction status, match status, reviewed

**Flagged Candidate**:
A candidate set aside for further attention without a final decision.
_Avoid_: Reviewed candidate, rejected candidate

**Shortlisted Candidate**:
A candidate HR has chosen to advance and may contact using the shortlisted template.
_Avoid_: Accepted candidate

**Rejected Candidate**:
A candidate HR has decided not to advance and may contact using the rejected template.
_Avoid_: Deleted candidate

**Candidate Details**:
The editable candidate name, contact email, and contact phone for this submission. For form-sourced candidates the Form Layout's special roles pre-fill them at import (HR can always correct them — a form field is a convenience, not truth); for email-sourced candidates they start empty and are entered manually from review evidence.
_Avoid_: Source sender, master person profile

**Manual Requirement Review**:
HR's per-candidate confirmation that a vacancy requirement has been checked and appears satisfied; it is a saved human assessment, not a computed match.
_Avoid_: Requirement match, extracted skill, match score

**Triage Mode**:
The review workspace's default state, where no editable field has focus and the keyboard shortcuts for navigation and decisions are armed.
_Avoid_: Queue mode, browse mode

**Editing Mode**:
The review workspace state while a text field has focus; decision shortcuts stay inert until HR exits with Esc or N.
_Avoid_: Input mode, composer mode

**Vacancy Progress**:
The number of shortlisted and rejected candidates compared with all candidates in a vacancy except the Screened Out; screened-out candidates are outside HR's decision funnel and never count toward progress.
_Avoid_: Flagged count, match score

**Email Template**:
Optional editable subject and body text owned by one vacancy for either shortlisted or rejected candidates; reuse creates an independent copy. The text may carry two placeholders — the candidate's name and the vacancy's title — that resolve per candidate when a Prepared Message is generated; a candidate with neither a typed name nor a source sender name reads as a neutral "there" in the resolved message.
_Avoid_: Uploaded template file, shared template

**Requirement Match** (deferred to V3):
A case-insensitive word-boundary phrase match of a vacancy requirement against the primary CV's full extracted text, per ADR-0009.
_Avoid_: Fuzzy match, review decision, extracted skill

**Match** (deferred to V3):
The current number of a vacancy's requirements matched by a candidate compared with the vacancy's total requirements.
_Avoid_: Match status, stored score

**Prepared Message**:
A personalized message generated from an email template for one candidate for HR to send using their email client.
_Avoid_: Sent email, bulk email

**Contactable Candidate**:
A candidate eligible to receive a Prepared Message: a Rejected Candidate or a member of the Bench with a contact email recorded. New and flagged candidates, shortlisted candidates carrying a hire outcome, and candidates without a contact email are never contacted; the send flow names who was left out and why.
_Avoid_: Recipient, send-list member

**Needed Hires**:
The number of people a vacancy must ultimately hire, recorded on the vacancy; the system never closes a vacancy automatically, so a filled vacancy stays open until HR closes it. Intake rounds carry no quota of their own.
_Avoid_: Headcount request, batch size, round quota

**Filled**:
The display state of a vacancy whose active hires have reached its needed hires; the vacancy remains open.
_Avoid_: Auto-closed vacancy, completed vacancy

**Hire Outcome**:
The bookkeeping record of how a shortlisted candidate's hiring stands: none, hired, runaway, or declined. Set only on shortlisted candidates, editable until the vacancy closes, and never altered by review-status decisions; a runaway re-opens one needed-hire slot.
_Avoid_: Hiring status, review decision

**Hired Candidate**:
A shortlisted candidate who has started the job; the hire is active until they are marked as runaway.
_Avoid_: Accepted candidate, permanent employee

**Runaway**:
A hired candidate who no-showed, quit, or went unreachable while the vacancy is still open; the event reopens one needed-hire slot and is marked manually by HR when informed.
_Avoid_: Terminated employee, failed candidate

**Declined**:
The outcome when a shortlisted candidate exits on their own initiative: turning down the offer, withdrawing mid-process, or going silent before an offer exists; it does not reopen a needed-hire slot the way a runaway does.
_Avoid_: Rejected candidate, withdrawn candidate

**Bench**:
The shortlisted candidates on a vacancy who carry no hire outcome; they are undecided rather than rejected, and remain available as backfill when a hire goes runaway or for promotion into a later round.
_Avoid_: Talent pool, waitlist

**Shortage**:
A vacancy's needed hires minus its active hires; the number of slots still to fill, which increases again when a runaway is recorded.
_Avoid_: Gap, match score, vacancy progress