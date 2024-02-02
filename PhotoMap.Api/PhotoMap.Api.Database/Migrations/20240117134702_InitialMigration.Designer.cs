﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PhotoMap.Api.Database;
using PhotoMap.Api.Domain.Models;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    [DbContext(typeof(PhotoMapContext))]
    [Migration("20240117134702_InitialMigration")]
    partial class InitialMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.PhotoEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("AddedOn")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("added_on");

                    b.Property<DateTimeOffset>("DateTimeTaken")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("date_time_taken");

                    b.Property<string>("ExifString")
                        .HasColumnType("text")
                        .HasColumnName("exif_string");

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("file_name");

                    b.Property<bool>("HasGps")
                        .HasColumnType("boolean")
                        .HasColumnName("has_gps");

                    b.Property<double?>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double?>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<string>("Path")
                        .HasColumnType("text")
                        .HasColumnName("path");

                    b.Property<long>("PhotoSourceId")
                        .HasColumnType("bigint")
                        .HasColumnName("photo_source_id");

                    b.Property<string>("ThumbnailLargeFilePath")
                        .HasColumnType("text")
                        .HasColumnName("thumbnail_large_file_path");

                    b.Property<string>("ThumbnailSmallFilePath")
                        .HasColumnType("text")
                        .HasColumnName("thumbnail_small_file_path");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("pk_photos");

                    b.HasIndex("PhotoSourceId")
                        .HasDatabaseName("ix_photos_photo_source_id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("ix_photos_user_id");

                    b.ToTable("photos", (string)null);
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.PhotoSourceEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<ClientAuthSettings>("ClientAuthSettings")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("client_auth_settings");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("ServiceFactoryImplementationType")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("service_factory_implementation_type");

                    b.Property<string>("Settings")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("settings");

                    b.HasKey("Id")
                        .HasName("pk_photo_sources");

                    b.ToTable("photo_sources", (string)null);
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.HasKey("Id")
                        .HasName("pk_users");

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserPhotoSourceEntity", b =>
                {
                    b.Property<long>("UserId")
                        .HasColumnType("bigint")
                        .HasColumnName("user_id");

                    b.Property<long>("PhotoSourceId")
                        .HasColumnType("bigint")
                        .HasColumnName("photo_source_id");

                    b.Property<string>("ProcessingState")
                        .HasColumnType("text")
                        .HasColumnName("processing_state");

                    b.Property<UserAuthResult>("UserAuthResult")
                        .HasColumnType("jsonb")
                        .HasColumnName("user_auth_result");

                    b.HasKey("UserId", "PhotoSourceId")
                        .HasName("pk_users_photo_sources");

                    b.HasIndex("PhotoSourceId")
                        .HasDatabaseName("ix_users_photo_sources_photo_source_id");

                    b.ToTable("users_photo_sources", (string)null);
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserPhotoSourceStatusEntity", b =>
                {
                    b.Property<long>("UserId")
                        .HasColumnType("bigint")
                        .HasColumnName("user_id");

                    b.Property<long>("PhotoSourceId")
                        .HasColumnType("bigint")
                        .HasColumnName("photo_source_id");

                    b.Property<int>("FailedCount")
                        .HasColumnType("integer")
                        .HasColumnName("failed_count");

                    b.Property<DateTimeOffset?>("LastUpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_updated_at");

                    b.Property<int>("ProcessedCount")
                        .HasColumnType("integer")
                        .HasColumnName("processed_count");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<int>("TotalCount")
                        .HasColumnType("integer")
                        .HasColumnName("total_count");

                    b.HasKey("UserId", "PhotoSourceId")
                        .HasName("pk_users_photo_sources_status");

                    b.HasIndex("PhotoSourceId")
                        .HasDatabaseName("ix_users_photo_sources_status_photo_source_id");

                    b.ToTable("users_photo_sources_status", (string)null);
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.PhotoEntity", b =>
                {
                    b.HasOne("PhotoMap.Api.Database.Entities.PhotoSourceEntity", "PhotoSource")
                        .WithMany()
                        .HasForeignKey("PhotoSourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_photos_photo_sources_photo_source_id");

                    b.HasOne("PhotoMap.Api.Database.Entities.UserEntity", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_photos_users_user_id");

                    b.Navigation("PhotoSource");

                    b.Navigation("User");
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserPhotoSourceEntity", b =>
                {
                    b.HasOne("PhotoMap.Api.Database.Entities.PhotoSourceEntity", "PhotoSource")
                        .WithMany("UserPhotoSourcesStates")
                        .HasForeignKey("PhotoSourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_users_photo_sources_photo_sources_photo_source_id");

                    b.HasOne("PhotoMap.Api.Database.Entities.UserEntity", "User")
                        .WithMany("UserPhotoSourcesStates")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_users_photo_sources_users_user_id");

                    b.Navigation("PhotoSource");

                    b.Navigation("User");
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserPhotoSourceStatusEntity", b =>
                {
                    b.HasOne("PhotoMap.Api.Database.Entities.PhotoSourceEntity", "PhotoSource")
                        .WithMany("UserPhotoSourcesStatuses")
                        .HasForeignKey("PhotoSourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_users_photo_sources_status_photo_sources_photo_source_id");

                    b.HasOne("PhotoMap.Api.Database.Entities.UserEntity", "User")
                        .WithMany("UserPhotoSourcesStatuses")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_users_photo_sources_status_users_user_id");

                    b.Navigation("PhotoSource");

                    b.Navigation("User");
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.PhotoSourceEntity", b =>
                {
                    b.Navigation("UserPhotoSourcesStates");

                    b.Navigation("UserPhotoSourcesStatuses");
                });

            modelBuilder.Entity("PhotoMap.Api.Database.Entities.UserEntity", b =>
                {
                    b.Navigation("UserPhotoSourcesStates");

                    b.Navigation("UserPhotoSourcesStatuses");
                });
#pragma warning restore 612, 618
        }
    }
}