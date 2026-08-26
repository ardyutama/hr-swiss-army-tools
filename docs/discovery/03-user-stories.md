# User Stories

Derived from the problem statement and to-be process.

## Functional stories

| ID  | Story | Source |
| --- | ----- | ------ |
| US-1 | As an HR staff member, I want incoming CVs collected from email into one place, so that I don't have to open every email and check subjects one by one. | Solve #1 |
| US-2 | As an HR staff member, I want a tidy, straightforward CV review workspace, so that reviewing takes less effort. | Solve #2 |
| US-3 | As an HR staff member, I want to take notes or mark a CV directly inside the review workspace, so that my notes stay attached to the CV. | Solve #2 |
| US-4 | As an HR staff member, I want CVs sortable against the mixed variety of job requirements, so that I don't sort them one by one manually. | Pain: mixed job requirements, sort one by one |
| US-5 | As an HR staff member, I want to create a message template and send it based on the chosen talent's resume, so that contacting talent is part of the same flow. | Solve #3 |

## Environment / non-functional stories

| ID  | Story | Source |
| --- | ----- | ------ |
| US-6 | As an HR staff member with no IT background on a Windows machine, I want the tool to work with straightforward instructions and no setup overhead. | HR environment #1, #2 |
| US-7 | As an HR staff member, I want further requests or updates to integrate right away with just a click. | HR environment #3 |
| US-8 | As an HR staff member, I want the tool to be fast and get the job done. | HR environment #4 |

## UI-sketch stories (from 05-ui-sketches.md)

### Vacancies

| ID   | Story | Source |
| ---- | ----- | ------ |
| US-9 | As an HR staff member, I want to create a vacancy with a role title, date, and skills requirements, so that incoming CVs have somewhere to go. | S1 Add Vacancies |
| US-10 | As an HR staff member, I want to see all vacancies with status and progress (e.g. 3/30 candidates processed), so that I know where each hiring effort stands. | S1 table |
| US-11 | As an HR staff member, I want to edit or delete a vacancy, so that I can fix mistakes and clean up old roles. | S1 Edit/Delete |

### Import

| ID   | Story | Source |
| ---- | ----- | ------ |
| US-12 | As an HR staff member, I want to drop exported `.eml` files into a vacancy, so that candidates are imported without me opening each email. | S2 drop zone |
| US-13 | As an HR staff member, I want each imported email to become a candidate with its PDF attachment(s), subject, and body preserved, so that nothing from the original email is lost. | S2, S4 email panel |

### Review

| ID   | Story | Source |
| ---- | ----- | ------ |
| US-14 | As an HR staff member, I want a candidate list per vacancy showing name, match status, notes, and status, so that I can see the whole pipeline at a glance. | S3 |
| US-15 | As an HR staff member, I want a review screen showing the PDF (multi-page, paginated) next to the vacancy's skill requirements and the candidate's extracted data (name, skills, email, phone), so that I can judge fit without switching windows. | S4 |
| US-16 | As an HR staff member, I want to see the candidate's original email subject and body in the review screen, so that I have their own words as context. | S4 email panel |
| US-17 | As an HR staff member, I want to Accept, Flag, or Reject a candidate and jump to the next one, so that reviewing 30 CVs is a fast repeated rhythm. | S4 action bar |
| US-18 | As an HR staff member, I want notes saved per candidate and a match status computed from skills-vs-requirements, so that my reasoning is captured and sortable. | S4 notes, S3 match status |

### Contact

| ID   | Story | Source |
| ---- | ----- | ------ |
| US-19 | As an HR staff member, I want one email template per vacancy (reusable from previous vacancies) for shortlisted and rejected candidates, sent to all in one action, so that closing the loop takes minutes not hours. | S5 |
