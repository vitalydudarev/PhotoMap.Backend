using System;
using System.Text.Json;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api;

public static class SeedDatabaseUtil
{
    public static void SeedDatabase(PhotoMapContext context)
    {
        context.Users.Add(new UserEntity { Id = 1, Name = "Vitaly" });

        context.PhotoSources.Add(CreateDropboxEntity());
        context.PhotoSources.Add(CreateYandexDiskEntity());

        context.SaveChanges();
    }
    
    private static PhotoSourceEntity CreateDropboxEntity()
    {
        var dropboxAuthSettings = new AuthSettings
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
        var yandexDiskAuthSettings = new AuthSettings
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

    private static PhotoSourceEntity CreatePhotoSource<TSettings>(long id, string name, TSettings settings, AuthSettings authSettings, Type serviceImplementationType)
    {
        return new PhotoSourceEntity
        {
            Id = id,
            Name = name,
            Settings = JsonSerializer.Serialize(settings),
            AuthSettings = authSettings,
            ServiceFactoryImplementationType = GetImplementationClassName(serviceImplementationType)
        };
    }
        
    private static string GetImplementationClassName(Type type)
    {
        return $"{type.FullName}, {type.Assembly.FullName}";
    }
}