using Android.Content;
using Android.Provider;
using PocketVault.Android.Services;
using PocketVault.Core;
using Uri = Android.Net.Uri;

namespace PocketVault.Android;

internal sealed class SafDocumentAccess : IDocumentAccess
{
    private static ContentResolver Resolver => global::Android.App.Application.Context.ContentResolver!;

    public async Task<VaultDocument?> PickAsync()
    {
        using var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, false);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        string? id = await ShowAsync(intent);
        return id is null ? null : await Task.Run(() => ReadMetadata(id));
    }

    public async Task<VaultDocument?> CreateAsync(string suggestedName)
    {
        VaultFormat.ValidateFileName(suggestedName);
        using var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/octet-stream");
        intent.PutExtra(Intent.ExtraTitle, suggestedName);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        string? id = await ShowAsync(intent);
        // Do not query after creation: a metadata failure would leave a document untracked.
        return id is null ? null : new VaultDocument(id, suggestedName, null);
    }

    private static Task<string?> ShowAsync(Intent intent) => MainThread.InvokeOnMainThreadAsync(() =>
        (Platform.CurrentActivity as MainActivity ?? throw new InvalidOperationException("Activity недоступна."))
            .SelectDocumentAsync(intent));

    private static VaultDocument ReadMetadata(string id)
    {
        using var uri = ContentUri(id);
        using var cursor = Resolver.Query(uri, [IOpenableColumns.DisplayName!, IOpenableColumns.Size!], null, null, null);
        if (cursor is null || !cursor.MoveToFirst())
            throw new VaultException("Не удалось получить имя выбранного документа. Выберите файл заново.");
        int nameColumn = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
        int sizeColumn = cursor.GetColumnIndex(IOpenableColumns.Size);
        string? name = nameColumn < 0 || cursor.IsNull(nameColumn) ? null : cursor.GetString(nameColumn);
        if (string.IsNullOrEmpty(name)) throw new VaultException("Провайдер не сообщил имя документа.");
        long? size = sizeColumn < 0 || cursor.IsNull(sizeColumn) ? null : cursor.GetLong(sizeColumn);
        if (size < 0) size = null;
        var document = new VaultDocument(id, name, size);
        DocumentOperations.ValidateDocument(document);
        return document;
    }

    internal static Task<VaultDocument> OpenContainerAsync(string id) => Task.Run(() =>
    {
        var document = ReadMetadata(id);
        if (!document.IsContainer) throw new VaultException("Откройте контейнер с расширением .pvlt.");
        // Validate the temporary read grant now; recheck it when the operation starts.
        using var stream = new SafDocumentAccess().OpenRead(document);
        return document;
    });

    public Stream OpenRead(VaultDocument document)
    {
        using var uri = ContentUri(document.Id);
        return Resolver.OpenInputStream(uri) ?? throw new IOException("Провайдер не открыл документ.");
    }

    public Stream OpenWrite(VaultDocument document)
    {
        using var uri = ContentUri(document.Id);
        return Resolver.OpenOutputStream(uri, "wt") ?? throw new IOException("Провайдер не открыл новый документ.");
    }

    public bool TryDeleteCreated(VaultDocument document)
    {
        using var uri = ContentUri(document.Id);
        // Called only for a newly returned CREATE_DOCUMENT URI after failed writing.
        // Android checks the grant; unsupported deletion or revoked grants are handled by the caller.
        return DocumentsContract.IsDocumentUri(global::Android.App.Application.Context, uri) &&
            DocumentsContract.DeleteDocument(Resolver, uri);
    }

    private static Uri ContentUri(string id)
    {
        var uri = Uri.Parse(id) ?? throw new VaultException("Провайдер вернул недопустимый URI.");
        if (uri.Scheme == "content") return uri;
        uri.Dispose();
        throw new VaultException("Нужен документ с content:// URI. Выберите его через системный диалог.");
    }
}
