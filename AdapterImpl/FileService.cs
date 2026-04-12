using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.File.Requests;
using ShiroBot.Model.File.Responses;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class FileService : IFileService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task<UploadPrivateFileResponse> UploadPrivateFileAsync(UploadPrivateFileRequest request) =>
        Milky.RequestAsync<UploadPrivateFileRequest, UploadPrivateFileResponse>(request);

    public Task<UploadGroupFileResponse> UploadGroupFileAsync(UploadGroupFileRequest request) =>
        Milky.RequestAsync<UploadGroupFileRequest, UploadGroupFileResponse>(request);

    public Task<GetPrivateFileDownloadUrlResponse> GetPrivateFileDownloadUrlAsync(GetPrivateFileDownloadUrlRequest request) =>
        Milky.RequestAsync<GetPrivateFileDownloadUrlRequest, GetPrivateFileDownloadUrlResponse>(request);

    public Task<GetGroupFileDownloadUrlResponse> GetGroupFileDownloadUrlAsync(GetGroupFileDownloadUrlRequest request) =>
        Milky.RequestAsync<GetGroupFileDownloadUrlRequest, GetGroupFileDownloadUrlResponse>(request);

    public Task<GetGroupFilesResponse> GetGroupFilesAsync(GetGroupFilesRequest request) =>
        Milky.RequestAsync<GetGroupFilesRequest, GetGroupFilesResponse>(request);

    public Task MoveGroupFileAsync(MoveGroupFileRequest request) =>
        Milky.RequestAsync(request);

    public Task RenameGroupFileAsync(RenameGroupFileRequest request) =>
        Milky.RequestAsync(request);

    public Task DeleteGroupFileAsync(DeleteGroupFileRequest request) =>
        Milky.RequestAsync(request);

    public Task<CreateGroupFolderResponse> CreateGroupFolderAsync(CreateGroupFolderRequest request) =>
        Milky.RequestAsync<CreateGroupFolderRequest, CreateGroupFolderResponse>(request);

    public Task RenameGroupFolderAsync(RenameGroupFolderRequest request) =>
        Milky.RequestAsync(request);

    public Task DeleteGroupFolderAsync(DeleteGroupFolderRequest request) =>
        Milky.RequestAsync(request);
}
