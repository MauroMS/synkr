﻿using CloudSynkr.Models;
using CloudSynkr.Models.Extensions;
using CloudSynkr.Repositories.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using File = CloudSynkr.Models.File;

namespace CloudSynkr.Repositories;

public class CloudStorageRepository : ICloudStorageRepository
{
    public async Task<string> CreateFolder(string folderName, UserCredential credentials, string parentId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("CreateFolder");

        using var driveService = new DriveService(new BaseClientService.Initializer()
            { HttpClientInitializer = credentials, ApplicationName = "Synkr" });

        // File metadata
        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = new List<string>
            {
                parentId
            }
        };

        // Create a new folder on drive.
        var request = driveService.Files.Create(fileMetadata);
        request.Fields = "id";
        var file = await request.ExecuteAsync(cancellationToken);
        // Prints the created folder id.
        Console.WriteLine("Folder ID: " + file.Id);
        Console.WriteLine("Folder Name: " + file.Name);

        return file.Id;
    }

    public async Task<Folder?> GetBasicFolderInfoByName(UserCredential credentials, string parentId, string folderName,
        CancellationToken cancellationToken)
    {
        using var driveService = new DriveService(new BaseClientService.Initializer()
            { HttpClientInitializer = credentials, ApplicationName = "Synkr" });

        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD
        var foldersRequest = driveService.Files.List();
        foldersRequest.Fields = "files(id, name, mimeType, modifiedTime, parents)";
        foldersRequest.Q =
            $"name = '{folderName}' and mimeType = '{FileType.Folder}' and trashed=false and '{parentId}' in parents";
        var listFoldersRequest = await foldersRequest.ExecuteAsync(cancellationToken);
        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD

        var folder = listFoldersRequest.Files.FirstOrDefault();

        return folder?.MapFolder();        
    }

    public async Task<Folder?> GetBasicFolderInfoById(UserCredential credentials, string folderId,
        CancellationToken cancellationToken)
    {
        //TODO: DO I NEED THIS METHOD??
        using var driveService = new DriveService(new BaseClientService.Initializer()
            { HttpClientInitializer = credentials, ApplicationName = "Synkr" });
    
        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD
        var foldersRequest = driveService.Files.List();
        foldersRequest.Fields = "files(id, name, mimeType, modifiedTime, parents)";
        foldersRequest.Q = $"mimeType = '{FileType.Folder}' and trashed=false";
        var listFoldersRequest = await foldersRequest.ExecuteAsync(cancellationToken);
        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD
    
        var folder = listFoldersRequest.Files.FirstOrDefault(folder => folder.Id == folderId);
    
        return folder?.MapFolder();
    }

    public async Task<List<Folder>> GetAllFoldersByParentId(UserCredential credentials, string folderId,
        string folderName, string parentId, string fullPath, CancellationToken cancellationToken)
    {
        var folders = new List<Folder>();
        var folder = new Folder()
        {
            Id = folderId,
            Name = folderName,
            Type = FileType.Folder,
            ParentId = parentId,
            Path = fullPath
        };

        using var driveService = new DriveService(new BaseClientService.Initializer()
            { HttpClientInitializer = credentials, ApplicationName = "Synkr" });

        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD
        var foldersRequest = driveService.Files.List();
        foldersRequest.Fields = "files(id, name, mimeType, modifiedTime, parents)";
        // foldersRequest.Q = $"mimeType = '{FileType.Folder}' and trashed=false and '{folderId}' in parents";
        foldersRequest.Q = $"trashed=false and '{folderId}' in parents";
        var listFoldersRequest = await foldersRequest.ExecuteAsync(cancellationToken);
        //TODO: MOVE THIS BLOCK TO ANOTHER METHOD

        folder.Files.AddRange(listFoldersRequest.Files.Where(f => f.MimeType != FileType.Folder).Select(f => new File()
        {
            Name = f.Name,
            Id = f.Id,
            ParentId = folderId,
            LastModified = f.ModifiedTimeDateTimeOffset,
            MimeType = f.MimeType,
            ParentName = folderName,
        }).ToList());

        foreach (var subFolderPath in listFoldersRequest.Files.Where(f => f.MimeType == FileType.Folder))
        {
            fullPath += @$"\{subFolderPath.Name}";
            var subFolders =
                await GetAllFoldersByParentId(credentials, subFolderPath.Id, subFolderPath.Name, folderId, fullPath,
                    cancellationToken);
            folder.Children.AddRange(subFolders);
        }

        folders.Add(folder);

        // folders.AddRange(listFoldersRequest.Files.Select(folder => new Folder()
        //     { Name = folder.Name, Id = folder.Id, ParentId = folder.Parents[0], Type = FileType.Folder }));

        // foreach (var fold in listFoldersRequest.Files)
        // {
        //     Console.WriteLine($"Id: {fold.Id}");
        //     Console.WriteLine($"Name: {fold.Name}");
        //     Console.WriteLine($"Type: {fold.MimeType}");
        //     if (fold.Parents != null)
        //         foreach (var parent in fold.Parents)
        //         {
        //             Console.WriteLine($"{parent}");
        //         }
        //
        //     Console.WriteLine($"-----------------------------------");
        //     Console.WriteLine($"-----------------------------------");
        // }

        return folders;
    }

    public async Task<List<File>> GetAllFilesFromFolder(UserCredential credentials, string parentId,
        string parentName, CancellationToken cancellationToken)
    {
        Console.WriteLine("GetAllFilesFromFolder");
        using var driveService = new DriveService(new BaseClientService.Initializer()
            { HttpClientInitializer = credentials, ApplicationName = "Synkr" });

        var filesRequest = driveService.Files.List();
        filesRequest.Fields = "files(id, name, mimeType, modifiedTime, parents)";
        filesRequest.Q = $"mimeType != '{FileType.Folder}' and trashed = false and '{parentId}' in parents";

        var listFilesRequest = await filesRequest.ExecuteAsync(cancellationToken);
        var files = listFilesRequest.Files.Select(f => new File()
        {
            Name = f.Name,
            Id = f.Id,
            ParentId = parentId,
            LastModified = f.ModifiedTimeDateTimeOffset,
            MimeType = f.MimeType,
            ParentName = parentName
        }).ToList();

        // foreach (var file in files)
        // {
        //     Console.WriteLine($"Id: {file.Id}");
        //     Console.WriteLine($"Name: {file.Name}");
        //     Console.WriteLine($"MymeType: {file.MimeType}");
        //     Console.WriteLine($"Last Modified: {file.LastModified}");
        //     Console.WriteLine($"Parent Id: {file.ParentId}");
        //     Console.WriteLine($"Parent Name: {file.ParentName}");
        // }

        return files;
    }

    public async Task<MemoryStream?> DownloadFile(string fileId, UserCredential credentials)
    {
        try
        {
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials,
                ApplicationName = "Synkr"
            });

            var request = service.Files.Get(fileId);
            var stream = new MemoryStream();

            // Add a handler which will be notified on progress changes.
            // It will notify on each chunk download and when the
            // download is completed or failed.
            request.MediaDownloader.ProgressChanged +=
                progress =>
                {
                    switch (progress.Status)
                    {
                        case DownloadStatus.Downloading:
                        {
                            Console.WriteLine(progress.BytesDownloaded);
                            break;
                        }
                        case DownloadStatus.Completed:
                        {
                            Console.WriteLine($"IDownloadProgress: {progress.Status} ({progress.BytesDownloaded})");
                            break;
                        }
                        case DownloadStatus.Failed:
                        {
                            Console.WriteLine("Download failed.");
                            break;
                        }
                    }
                };
            await request.DownloadAsync(stream);

            return stream;
        }
        catch (Exception e)
        {
            // TODO(developer) - handle error appropriately
            if (e is AggregateException)
            {
                Console.WriteLine("Credential Not found");
            }
            else
            {
                throw;
            }
        }

        return null;
    }

    public string UploadNewFile(UserCredential credentials, string filePath, string name)
    {
        try
        {
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials,
                ApplicationName = "Synkr"
            });

            // Upload file photo.jpg on drive.
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = name
            };
            FilesResource.CreateMediaUpload request;
            var path = Path.Combine(filePath, name);
            // Create a new file on drive.
            using (var stream = new FileStream(path,
                       FileMode.Open))
            {
                // Create a new file, with metadata and stream.
                request = service.Files.Create(
                    fileMetadata, stream, "text/plain");
                request.Fields = "id";
                request.Upload();
            }

            var file = request.ResponseBody;
            // Prints the uploaded file id.
            Console.WriteLine("File ID: " + file.Id);
            return file.Id;
        }
        catch (Exception e)
        {
            // TODO(developer) - handle error appropriately
            if (e is AggregateException)
            {
                Console.WriteLine("Credential Not found");
            }
            else if (e is FileNotFoundException)
            {
                Console.WriteLine("File not found");
            }
            else
            {
                throw;
            }
        }

        return null;
    }

    public async Task<string> UpdateFile(UserCredential credentials, string filePath, File file)
    {
        try
        {
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credentials,
                ApplicationName = "Synkr"
            });

            // Note: not all fields are writeable watch out, you cant just send uploadedFile back.
            var updateFileBody = new Google.Apis.Drive.v3.Data.File()
            {
                Name = file.Name,
                MimeType = file.MimeType
            };
            // Then upload the file again with a new name and new data.
            await using (var uploadStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Update the file id, with new metadata and stream.
                var updateRequest = service.Files.Update(updateFileBody, file.Id, uploadStream, file.MimeType);
                var result = await updateRequest.UploadAsync(CancellationToken.None);

                switch (result.Status)
                {
                    case UploadStatus.Starting:
                        Console.WriteLine($"Start to Upload file: '{file.Name}' to '{file.ParentName}'");
                        break;
                    case UploadStatus.Uploading:
                        Console.WriteLine($"Uploading file: '{file.Name}' to '{file.ParentName}'");
                        break;
                    case UploadStatus.Completed:
                        Console.WriteLine($"File '{file.Name}' successfully uploaded to '{file.ParentName}'");
                        break;
                    case UploadStatus.Failed:
                        Console.WriteLine($"Error uploading file '{file.Name}': {result.Exception.Message}");
                        break;
                }
            }

            return file.Id;
        }
        catch (Exception e)
        {
            // TODO(developer) - handle error appropriately
            if (e is AggregateException)
            {
                Console.WriteLine("Credential Not found");
            }
            else if (e is FileNotFoundException)
            {
                Console.WriteLine("File not found");
            }
            else
            {
                throw;
            }
        }

        return null;
    }
}