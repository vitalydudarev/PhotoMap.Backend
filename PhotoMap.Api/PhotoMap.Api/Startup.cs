using System;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Repositories;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Hubs;
using PhotoMap.Api.Middlewares;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Factories;
using PhotoMap.Api.Services.Implementations;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;
using PhotoMap.Api.Services.Services.Domain;
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

            // Brotli, else gzip, for the JSON and text responses. The responses carry no secrets that a request
            // could reflect alongside, so compressing over HTTPS is safe from BREACH.
            services.AddResponseCompression(options => options.EnableForHttps = true);
            // Optimal over the default Fastest: a response is compressed once, the extra time is small next to its
            // download
            services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
            services.Configure<FileStorageSettings>(Configuration.GetSection("FileStorage"));
            services.Configure<PhotoProcessingSettings>(Configuration.GetSection("PhotoProcessing"));

            services.AddHttpClient();

            services.AddScoped<IImageStore, ImageStore>();
            services.AddScoped<IPhotoProvider, PhotoProvider>();

            services.AddScoped<IPhotoSourceDownloadServiceFactory, PhotoSourceDownloadServiceFactory>();
            services.AddScoped<IPhotoSourceProcessingService, PhotoSourceProcessingService>();
            services.AddScoped<IPhotoSourceDataService, PhotoSourceDataService>();
            services.AddScoped<IDownloadServiceFactory, DropboxDownloadServiceFactory>();
            services.AddScoped<IDownloadServiceFactory, YandexDiskDownloadServiceFactory>();

            // domain services
            services.AddScoped<IPhotoService, PhotoService>();
            services.AddSingleton<PhotoYearsCache>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPhotoSourceService, PhotoSourceService>();
            services.AddScoped<IUserPhotoSourceService, UserPhotoSourceService>();
            services.AddScoped<IFailedFileService, FailedFileService>();
            
            // repositories
            services.AddScoped<IPhotoRepository, PhotoRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPhotoSourceRepository, PhotoSourceRepository>();

            // database context
            services.AddDbContext<PhotoMapContext>();
            
            // the state photo source runs resume from
            services.AddScoped(typeof(IDownloadStateService<>), typeof(DownloadStateService<>));
            
            // common
            services.AddSingleton<BackgroundTaskManager>();
            services.AddSingleton<IBackgroundTaskManager>(provider => provider.GetRequiredService<BackgroundTaskManager>());
            
            services.AddScoped<IFileStorage, FileStorage>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<FileStorageSettings>>().Value;
                
                return new FileStorage(settings);
            });

            services.AddScoped<HostInfo>();

            services.AddSingleton<IFrontendNotificationService, FrontendNotificationService>();
            
            // worker, hosted in this application
            services.AddWorker(Configuration);
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

            // first, so that every response, error responses included, is compressed
            app.UseResponseCompression();

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
            PauseInterruptedRuns(app);
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

        /// <summary>
        /// A run records its final status when it finishes, also when the application stops gracefully. When the
        /// application is killed or crashes, the run is left recorded as in progress while nothing runs it.
        /// Called before the server starts, so no run can have been started yet.
        /// </summary>
        private static void PauseInterruptedRuns(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var userPhotoSourceService = scope.ServiceProvider.GetRequiredService<IUserPhotoSourceService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();

            var pausedCount = userPhotoSourceService.PauseInProgressAsync().GetAwaiter().GetResult();
            if (pausedCount > 0)
            {
                logger.LogInformation("Paused {PausedCount} photo source runs interrupted by the application stopping", pausedCount);
            }
        }
    }
}
