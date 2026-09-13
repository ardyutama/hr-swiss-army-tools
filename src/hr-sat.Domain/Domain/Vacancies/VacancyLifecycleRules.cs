using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Vacancies;

internal static class VacancyLifecycleRules
{
    public static Result Close(VacancyStatus status)
    {
        var openResult = EnsureOpen(
            status,
            "A closed vacancy must be reopened before it can be closed again.");
        return openResult.IsFailure ? openResult : Result.Success();
    }

    public static Result Reopen(VacancyStatus status)
    {
        if (status != VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a closed vacancy can be reopened."]
            });
        }

        return Result.Success();
    }

    public static Result EnsureOpen(VacancyStatus status, string message)
    {
        if (status == VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = [message]
            });
        }

        return Result.Success();
    }

    public static Result EnsureCanReceiveCandidateImport(VacancyStatus status) =>
        EnsureOpen(status, "A closed vacancy cannot receive candidate imports.");

    public static Result EnsureCanRemoveCandidate(VacancyStatus status) =>
        EnsureOpen(
            status,
            "A closed vacancy must be reopened before candidates can be removed.");

    public static Result EnsureCanReviewCandidate(VacancyStatus status) =>
        EnsureOpen(
            status,
            "A closed vacancy must be reopened before candidates can be reviewed.");

    public static Result EnsureCanRecordHireOutcome(VacancyStatus status, long vacancyId) =>
        status == VacancyStatus.Closed
            ? VacancyErrors.Closed(vacancyId)
            : Result.Success();

    public static Result EnsureCanMutateEmailTemplate(VacancyStatus status, long vacancyId) =>
        status == VacancyStatus.Closed
            ? EmailTemplateErrors.VacancyClosed(vacancyId)
            : Result.Success();
}
