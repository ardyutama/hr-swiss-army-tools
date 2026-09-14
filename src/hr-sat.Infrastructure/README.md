# HR Swiss Army Tools

## Manual testing / demo data

When the API runs in Development, startup migrates the database and seeds four scenario vacancies:

| Vacancy | Manual scenarios |
| --- | --- |
| Field Technician - Surabaya | Requirement-review contrast, flagged notes, shortlisted/rejected states, no-contact fallback, multiple CVs, duplicate source email, and both templates |
| Warehouse Supervisor - Bekasi | Closed Round 1, active Round 2, promotion-eligible candidates, prior-application notice, and shortlisted template |
| People Operations Specialist | Open vacancy between rounds; its closed Round 1 makes imports unavailable |
| Product Designer | Closed vacancy with reviewed candidates and read-only behavior |

Seeded names describe the scenario they represent. The fixture `bambang-riyanto-no-attachment.eml` is not seeded; importing it manually verifies that a source email with no PDF is accepted. The Budi fixture is byte-identical to the seeded source for duplicate-import verification.

To reset the demo database to the known scenario set:

```powershell
dotnet ef database drop --force --project src/hr-sat.Infrastructure --startup-project src/hr-sat.Web.Api
dotnet run --project src/hr-sat.Web.Api
```

The REST Client walkthrough is in `src/hr-sat.Web.Api/hr-sat.Web.Api.http`. Scalar is available at `http://localhost:5086/scalar` while the API runs in Development.
