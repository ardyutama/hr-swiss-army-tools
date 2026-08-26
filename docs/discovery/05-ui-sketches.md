# UI Sketches — Screens

Translated from the five hand-drawn wireframes. These are the V1 screens; layout is
approximate, behaviour is authoritative.

## Global layout

Every screen has a left sidebar with an app icon and one nav item: **Sorting CV**. (More
nav items will appear as features grow.)

## S1 — Vacancy list

- Sidebar (global).
- Top area: two placeholder slots ("No Idea" — reserved, not built) and an **Add Vacancies**
  button.
- Main table, one row per vacancy:
  - **Vacancies roles** (title)
  - **Status Vacancies**
  - **Progress** (`3/30` — candidates processed / total)
  - **Date**
  - **Edit** / **Delete** actions

## S2 — Import CVs into a vacancy

- Header: **Back** | **Vacancies Title**
- A large drop zone: **"Drop eml export to import the data"**
- HR exports emails as `.eml` files from their mail client and drops them here.

## S3 — Candidate list per vacancy

- Header: **Back** | **Vacancies Title With date** | **Send Email to all candidate** button
- One card row per candidate:
  - **Candidate Name**
  - **Match Status**
  - **Notes**
  - **status**
  - **Send / Delete** actions

## S4 — Review workspace

- Header: **Vacancies Title** | position indicator **1/30 Candidates**
- Left column:
  - **Skills Requirements Vacancies** panel
  - **Candidate Data** — auto-extracted from the PDF: name, skills mentioned, email, number
  - **Subject and Body Emails from candidate** (expandable / "Open")
  - **Notes** editor with **Save**
- Right column: **PDF Viewer** — multi-page, with **Pagination PDF** controls
- Bottom action bar: **[Prev] [Accept] [Flagged] [Reject] [Next]**

## S5 — Bulk email with templates

- Modal/dialog titled **Vacancies Title With date**: **Send Email to all candidate**
- Two template sections, identical in shape:
  - **Shortlisted Email Template**: File Template + **View** / **Delete**, plus
    **Previous Template** pickers (two slots shown — reuse templates from previous vacancies)
  - **Rejected Email**: same shape
- **Send To All** button
- Rule: one template per vacancy, but templates are reusable across vacancies.
