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
using Microsoft.OpenApi;
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
using PhotoMap.Shared.Messaging.EventHandler;
using PhotoMap.Shared.Messaging.InProcess;
using PhotoMap.Worker;
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
            services.AddControllers();
            services.Configure<FileStorageSettings>(Configuration.GetSection("FileStorage"));
            services.Configure<StorageServiceSettings>(Configuration.GetSection("Storage"));
            services.Configure<PhotoProcessingSettings>(Configuration.GetSection("PhotoProcessing"));

            services.AddSingleton(provider => new UserInfo { UserId = 1, Name = "Vitaly" });

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
            services.AddSingleton<BackgroundTaskManager>();
            services.AddSingleton<IBackgroundTaskManager>(provider => provider.GetRequiredService<BackgroundTaskManager>());
            
            services.AddScoped<IFileStorage, FileStorage>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;
                
                return new FileStorage(settings);
            });

            // event handlers
            services.AddSingleton<IEventHandler, ImageConvertedHandler>();

            services.AddScoped<IStorageService, StorageServiceClient>();
            services.AddScoped<HostInfo>();
            services.AddScoped<IFileProvider, LocalFileProvider>();
            services.AddSingleton<IConvertedImageHolder, ConvertedImageHolder>();

            services.AddSingleton<IFrontendNotificationService, FrontendNotificationService>();
            
            // in-process messaging
            services.AddInProcessMessaging();

            // worker, hosted in this application
            services.AddWorker();
            services.AddHostedService<ProcessedImageBackgroundService>();
            // registered after the image processing services: hosted services stop in reverse order, so processing
            // runs are cancelled (and record their Stopped status) while the rest of the application is still running
            services.AddHostedService(provider => provider.GetRequiredService<BackgroundTaskManager>());
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "PhotoMap API V1", Version = "v1" });
                
                // generated client methods will have the same names as C# controllers
                c.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);
            });

            services.AddSignalR();
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
