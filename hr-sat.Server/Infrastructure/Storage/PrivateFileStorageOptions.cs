namespace hr_sat.Server.Infrastructure.Storage;

public sealed class PrivateFileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string RootPath { get; set; } = "private-files";
}