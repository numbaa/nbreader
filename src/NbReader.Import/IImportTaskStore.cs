namespace NbReader.Import;

public interface IImportTaskStore
{
    ImportTask? FindByNormalizedLocator(string normalizedLocator);

    void UpsertTask(ImportTask task);

    void AppendEvent(ImportTaskEvent taskEvent);
}
