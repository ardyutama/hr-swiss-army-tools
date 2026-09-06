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

**Promote**:
Moving a new, flagged, or shortlisted-without-outcome candidate from a closed round into the active round, preserving review status, requirement reviews, and notes.
_Avoid_: Copy candidate, re-import

**Prior Application Notice**:
A display-only indicator on a candidate that the same person applied in another round of the same vacancy; it never blocks import and never affects review status.
_Avoid_: Duplicate block, global person history

**Candidate**:
One person's submission to one intake round, created from one source email; the same person submitted to another round or vacancy is a different candidate.
_Avoid_: Talent, shared person, global candidate

**Source Email**:
The immutable exported email from which exactly one candidate is imported and whose original content is retained.
_Avoid_: Candidate email, live mailbox message

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
The editable candidate name and contact email for this submission. In V1 these fields start empty and are entered manually from the source email or other review evidence; PDF extraction is deferred to V3.
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
The number of shortlisted and rejected candidates compared with all candidates in a vacancy.
_Avoid_: Flagged count, match score

**Email Template**:
Optional editable subject and body text owned by one vacancy for either shortlisted or rejected candidates; reuse creates an independent copy.
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

**Needed Hires**:
The number of people a vacancy must ultimately hire, recorded on the vacancy; the system never closes a vacancy automatically, so a filled vacancy stays open until HR closes it. Intake rounds carry no quota of their own.
_Avoid_: Headcount request, batch size, round quota

**Filled**:
The display state of a vacancy whose active hires have reached its needed hires; the vacancy remains open.
_Avoid_: Auto-closed vacancy, completed vacancy

**Hired Candidate**:
A shortlisted candidate who has started the job; the hire is active until they are marked as runaway.
_Avoid_: Accepted candidate, permanent employee

**Runaway**:
A hired candidate who no-showed, quit, or went unreachable while the vacancy is still open; the event reopens one needed-hire slot and is marked manually by HR when informed.
_Avoid_: Terminated employee, failed candidate

**Declined**:
The outcome when a candidate turns down the offer; it does not reopen a needed-hire slot the way a runaway does.
_Avoid_: Rejected candidate, withdrawn candidate

**Shortage**:
A vacancy's needed hires minus its active hires; the number of slots still to fill, which increases again when a runaway is recorded.
_Avoid_: Gap, match score, vacancy progress