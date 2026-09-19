# Domain concept-cluster folders

**Status:** Accepted (2026-09-19)

The Domain layer groups files by concept cluster to improve scan-ability and module-boundary discovery. A cluster earns a plural subfolder when it has approximately five or more files and its own lifecycle or language; this creates `Vacancies/FormLayouts/` and `Candidates/FormResponses/`, while the candidate intake trio remains beside `Candidate` because it is the birth record shared by both intake sources. Screening remains flat under `Vacancies` under the deliberate placement in ADR-0017, and the existing top-level `EmailTemplates` folder is grandfathered. We reject grouping all vacancy-owned types together, and reject type-role folders such as `Rules/` and `Events/`, despite the glossary supporting those readings. Namespaces mirror the folder structure.
