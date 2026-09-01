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

**Candidate**:
One person's submission to one vacancy, created from one source email; the same person submitted to another vacancy is a different candidate.
_Avoid_: Talent, shared person, global candidate

**Source Email**:
The immutable exported email from which exactly one candidate is imported and whose original content is retained.
_Avoid_: Candidate email, live mailbox message

**Source Sender**:
The sender recorded by the source email, who may differ from the candidate.
_Avoid_: Candidate contact

**CV Document**:
A PDF attachment retained from a candidate's source email; a candidate may have several CV documents.
_Avoid_: Non-PDF attachment, source email

**Primary CV**:
The one CV document selected as the candidate's main document for extraction and review.
_Avoid_: First attachment

**Extraction Status**:
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
The editable candidate name and contact information initially extracted from the primary CV for this submission.
_Avoid_: Source sender, master person profile

**Candidate Skill**:
A distinct, editable, ordered phrase initially extracted from a candidate's primary CV.
_Avoid_: Vacancy requirement, global skill

**Vacancy Progress**:
The number of shortlisted and rejected candidates compared with all candidates in a vacancy.
_Avoid_: Flagged count, match score

**Email Template**:
Optional editable subject and body text owned by one vacancy for either shortlisted or rejected candidates; reuse creates an independent copy.
_Avoid_: Uploaded template file, shared template

**Requirement Match**:
An exact match between a vacancy requirement and a candidate skill after trimming and case-insensitive comparison.
_Avoid_: Fuzzy match, review decision

**Match**:
The current number of a vacancy's requirements matched by a candidate compared with the vacancy's total requirements.
_Avoid_: Match status, stored score

**Prepared Message**:
A personalized message generated from an email template for one candidate for HR to send using their email client.
_Avoid_: Sent email, bulk email