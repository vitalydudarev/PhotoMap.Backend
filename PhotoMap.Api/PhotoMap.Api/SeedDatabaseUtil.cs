using System;
using System.Text.Json;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api;

public static class SeedDatabaseUtil
{
    public static void SeedDatabase(PhotoMapContext context)
    {
        // User
        var userEntity = new UserEntity { Id = 1, Name = "Vitaly" };

        // Photo Sources
        var dropboxPhotoSource = CreateDropboxEntity();
        var yandexDiskPhotoSource = CreateYandexDiskEntity();
        
        // User - Photo Sources Auth
        var dropboxUserPhotoSourceAuth = CreateUserPhotoSourceAuth(userEntity.Id, dropboxPhotoSource.Id);
        var yandexDiskUserPhotoSourceAuth = CreateUserPhotoSourceAuth(userEntity.Id, yandexDiskPhotoSource.Id);
        
        // User - Photo Sources Status
        var dropboxUserPhotoSourceStatus = CreateUserPhotoSourceStatus(userEntity.Id, dropboxPhotoSource.Id);
        var yandexDiskUserPhotoSourceStatus = CreateUserPhotoSourceStatus(userEntity.Id, yandexDiskPhotoSource.Id);
        
        // Save to DB
        context.Users.Add(userEntity);
        context.PhotoSources.AddRange(dropboxPhotoSource, yandexDiskPhotoSource);
        context.UserPhotoSourcesAuth.AddRange(dropboxUserPhotoSourceAuth, yandexDiskUserPhotoSourceAuth);
        context.UserPhotoSourcesStatuses.AddRange(dropboxUserPhotoSourceStatus, yandexDiskUserPhotoSourceStatus);
        context.SaveChanges();
    }
    
    private static PhotoSourceEntity CreateDropboxEntity()
    {
        var dropboxAuthSettings = new ClientAuthSettings
        {
            OAuthConfiguration = new OAuthConfiguration
            {
                ClientId = "8pakfnac86x0iad",
                RedirectUri = "http://localhost:4200/dropbox",
                ResponseType = "code",
                AuthorizeUrl = "https://www.dropbox.com/oauth2/authorize",
                TokenUrl = "https://www.dropbox.com/oauth2/token",
                Scope = "account_info.read files.metadata.read files.content.read"
            },
            RelativeAuthUrl = "/dropbox"
        };
            
        var dropboxSettings = new DropboxSettings
        {
            SourceFolder = "/Camera Uploads",
            DownloadLimit = 2000
        };

        return CreatePhotoSource(1, "Dropbox", dropboxSettings, dropboxAuthSettings, typeof(DropboxDownloadServiceFactory));
    }

    private static PhotoSourceEntity CreateYandexDiskEntity()
    {
        var yandexDiskAuthSettings = new ClientAuthSettings
        {
            OAuthConfiguration = new OAuthConfiguration
            {
                ClientId = "66de926ff5be4d2da65e5eb64435687b",
                RedirectUri = "http://localhost:4200/yandex-disk",
                ResponseType = "token",
                AuthorizeUrl = "https://oauth.yandex.ru/authorize"
            },
            RelativeAuthUrl = "/yandex-disk"
        };
            
        var yandexDiskSettings = new YandexDiskSettings
        {
            UsePhotoStreamFolder = true,
            DownloadLimit = 100
        };

        return CreatePhotoSource(2, "Yandex.Disk", yandexDiskSettings, yandexDiskAuthSettings, typeof(YandexDiskDownloadServiceFactory));
    }

    private static PhotoSourceEntity CreatePhotoSource<TSettings>(long id, string name, TSettings settings, ClientAuthSettings clientAuthSettings, Type serviceImplementationType)
    {
        return new PhotoSourceEntity
        {
            Id = id,
            Name = name,
            Settings = JsonSerializer.Serialize(settings),
            ClientAuthSettings = clientAuthSettings,
            ServiceFactoryImplementationType = TypeHelper.GetTypeFullName(serviceImplementationType)
        };
    }

    private static UserPhotoSourceAuthEntity CreateUserPhotoSourceAuth(long userId, long photoSourceId)
    {
        return new UserPhotoSourceAuthEntity
        {
            UserId = userId,
            PhotoSourceId = photoSourceId
        };
    }
    
    private static UserPhotoSourceStatusEntity CreateUserPhotoSourceStatus(long userId, long photoSourceId)
    {
        return new UserPhotoSourceStatusEntity
        {
            UserId = userId,
            PhotoSourceId = photoSourceId,
            Status = PhotoSourceStatus.NotStarted
        };
    }
}