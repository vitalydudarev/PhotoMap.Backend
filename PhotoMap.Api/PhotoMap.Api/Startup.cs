using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NATS.Client;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Repositories;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Handlers;
using PhotoMap.Api.Hubs;
using PhotoMap.Api.Middlewares;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Implementations;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;
using PhotoMap.Api.Services.Services.Domain;
using PhotoMap.Api.Settings;
using PhotoMap.Shared;
using PhotoMap.Shared.Messaging;
using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.EventHandlerManager;
using PhotoMap.Shared.Messaging.MessageListener;
using PhotoMap.Shared.Messaging.MessageSender;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PhotoMap.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var natsUrl = GetConfigurationProperty("NatsUrl");
            
            services.AddControllers();
            services.Configure<FileStorageSettings>(Configuration.GetSection("FileStorage"));
            services.Configure<RabbitMqSettings>(Configuration.GetSection("RabbitMQ"));
            services.Configure<StorageServiceSettings>(Configuration.GetSection("Storage"));
            services.Configure<YandexDiskFileProviderSettings>(Configuration.GetSection("YandexDiskFileProvider"));
            services.Configure<PhotoProcessingSettings>(Configuration.GetSection("PhotoProcessing"));

            services.AddSingleton(provider => new UserInfo { UserId = 1, Name = "Vitaly" });

            services.AddSingleton(a =>
            {
                var settings = a.GetRequiredService<IOptions<RabbitMqSettings>>().Value;

                return new RabbitMqConfiguration
                {
                    HostName = settings.HostName,
                    Port = settings.Port,
                    UserName = settings.UserName,
                    Password = settings.Password,
                    ConsumeQueueName = settings.ResultsQueueName,
                    ResponseQueueName = settings.CommandsQueueName
                };
            });

            services.AddHttpClient();

            services.AddScoped<IImageStore, ImageStore>();
            services.AddScoped<IPhotoProvider, PhotoProvider>();

            services.AddScoped<IPhotoSourceDownloadServiceFactory, PhotoSourceDownloadServiceFactory>();
            services.AddScoped<IPhotoSourceProcessingService, PhotoSourceProcessingService>();
            services.AddScoped<IDownloadServiceFactory, DropboxDownloadServiceFactory>();
            services.AddScoped<IDownloadServiceFactory, YandexDiskDownloadServiceFactory>();

            // domain services
            services.AddScoped<IPhotoService, PhotoService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPhotoSourceService, PhotoSourceService>();
            services.AddScoped<IUserPhotoSourceService, UserPhotoSourceService>();
            
            // repositories
            services.AddScoped<IPhotoRepository, PhotoRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPhotoSourceRepository, PhotoSourceRepository>();

            // database context
            services.AddDbContext<PhotoMapContext>();
            
            // dropbox services
            services.AddScoped<IDropboxDownloadStateService, DropboxDownloadStateService>();
            
            // common
            services.AddSingleton<IProgressReporter, ProgressReporter>();
            services.AddSingleton<IBackgroundTaskManager, BackgroundTaskManager>();
            
            services.AddScoped<IFileStorage, FileStorage>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;
                
                return new FileStorage(settings);
            });

            // services.AddHostedService<HostedService>();

            // hubs
            services.AddSingleton<YandexDiskHub>();
            services.AddSingleton<DropboxHub>();
            services.AddSingleton<NotificationHub>();

            // event handlers
            services.AddSingleton<IEventHandler, ProgressMessageHandler>();
            services.AddSingleton<IEventHandler, ImageProcessedEventHandler>();
            services.AddSingleton<IEventHandler, NotificationHandler>();
            
            services.AddSingleton<IMessageSender, RabbitMqMessageSender>();
            services.AddSingleton<IMessageListener, RabbitMqMessageListener>();
            services.AddSingleton<IEventHandlerManager, EventHandlerManager>();
            services.AddScoped<IStorageService, StorageServiceClient>();
            services.AddScoped<HostInfo>();
            services.AddScoped<IFileProvider, LocalFileProvider>();
            services.AddSingleton<IConvertedImageHolder, ConvertedImageHolder>();

            services.AddSingleton<IFrontendNotificationService, FrontendNotificationService>();
            
            // messaging
            services.AddSingleton<IMessagingService, NatsMessagingService>();
            services.AddHostedService<NatsBackgroundService>();
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "PhotoMap API V1", Version = "v1" });
                
                // generated client methods will have the same names as C# controllers
                c.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);
            });

            services.AddSignalR();

            services.AddNats(options =>
            {
                options.Url = natsUrl;
                options.AsyncErrorEventHandler += AsyncErrorEventHandler;
            });
        }

        private void AsyncErrorEventHandler(object? sender, ErrEventArgs e)
        {
            int a = 1;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMiddleware<HostInfoMiddleware>();
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseHttpsRedirection();

            app.UseSerilogRequestLogging();

            app.UseCors(builder => builder
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<YandexDiskHub>("/yandex-disk-hub");
                endpoints.MapHub<DropboxHub>("/dropbox-hub");
                endpoints.MapHub<NotificationHub>("/notifications");
                endpoints.MapControllers();
            });

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhotoMap API V1");
            });

            ApplyDatabaseMigrations(app);
        }
        
        private string GetConfigurationProperty(string name)
        {
            return Configuration[name] ?? throw new Exception($"Configuration property {name} not specified.");
        }
        
        private static void ApplyDatabaseMigrations(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<PhotoMapContext>();

            var dbExists = context.GetService<IDatabaseCreator>().CanConnect();

            if (context.Database.IsRelational() && context.Database.GetPendingMigrations().Any())
            {
                context.Database.Migrate();
            }

            if (!dbExists)
            {
                SeedDatabaseUtil.SeedDatabase(context);
            }
        }
    }
}
