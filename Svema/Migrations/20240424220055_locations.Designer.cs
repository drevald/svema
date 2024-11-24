﻿// <auto-generated />
using System;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace svema.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20240424220055_locations")]
    partial class locations
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Data.Album", b =>
                {
                    b.Property<int>("AlbumId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("AlbumId"));

                    b.Property<float>("Latitude")
                        .HasColumnType("real")
                        .HasColumnName("latitude");

                    b.Property<int>("LocationPrecisionMeters")
                        .HasColumnType("integer")
                        .HasColumnName("location_precision_meters");

                    b.Property<float>("Longitude")
                        .HasColumnType("real")
                        .HasColumnName("longitude");

                    b.Property<string>("Name")
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<int>("PreviewId")
                        .HasColumnType("integer")
                        .HasColumnName("preview_id");

                    b.Property<int?>("UserId")
                        .HasColumnType("integer");

                    b.Property<int>("Zoom")
                        .HasColumnType("integer")
                        .HasColumnName("zoom");

                    b.HasKey("AlbumId");

                    b.HasIndex("UserId");

                    b.ToTable("albums");
                });

            modelBuilder.Entity("Data.AlbumComment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AlbumId")
                        .HasColumnType("integer")
                        .HasColumnName("album_id");

                    b.Property<int>("AuthorId")
                        .HasColumnType("integer")
                        .HasColumnName("author_id");

                    b.Property<string>("AuthorUsername")
                        .HasColumnType("text")
                        .HasColumnName("author_username");

                    b.Property<string>("Text")
                        .HasColumnType("text")
                        .HasColumnName("text");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("time");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("AuthorId");

                    b.ToTable("album_comments");
                });

            modelBuilder.Entity("Data.Location", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<float>("Latitude")
                        .HasColumnType("real")
                        .HasColumnName("latitude");

                    b.Property<int>("LocationPrecisionMeters")
                        .HasColumnType("integer")
                        .HasColumnName("location_precision_meters");

                    b.Property<float>("Longitude")
                        .HasColumnType("real")
                        .HasColumnName("longitude");

                    b.Property<string>("Name")
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<int>("Zoom")
                        .HasColumnType("integer")
                        .HasColumnName("zoom");

                    b.HasKey("Id");

                    b.ToTable("locations");
                });

            modelBuilder.Entity("Data.Person", b =>
                {
                    b.Property<int>("PersonId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("PersonId"));

                    b.Property<string>("FirstName")
                        .HasColumnType("text")
                        .HasColumnName("first_name");

                    b.Property<string>("LastName")
                        .HasColumnType("text")
                        .HasColumnName("last_name");

                    b.HasKey("PersonId");

                    b.ToTable("persons");
                });

            modelBuilder.Entity("Data.Shot", b =>
                {
                    b.Property<int>("ShotId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("ShotId"));

                    b.Property<int>("AlbumId")
                        .HasColumnType("integer")
                        .HasColumnName("album_id");

                    b.Property<string>("ContentType")
                        .HasColumnType("text")
                        .HasColumnName("content_type");

                    b.Property<DateTime>("DateEnd")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("date_end");

                    b.Property<DateTime>("DateStart")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("date_start");

                    b.Property<float>("Latitude")
                        .HasColumnType("real")
                        .HasColumnName("latitude");

                    b.Property<int?>("LocationId")
                        .HasColumnType("integer")
                        .HasColumnName("location_id");

                    b.Property<int>("LocationPrecisionMeters")
                        .HasColumnType("integer")
                        .HasColumnName("location_precision_meters");

                    b.Property<float>("Longitude")
                        .HasColumnType("real")
                        .HasColumnName("longitude");

                    b.Property<string>("MD5")
                        .HasColumnType("text")
                        .HasColumnName("md5");

                    b.Property<string>("Name")
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<byte[]>("Preview")
                        .HasColumnType("bytea")
                        .HasColumnName("preview");

                    b.Property<long>("Size")
                        .HasColumnType("bigint")
                        .HasColumnName("size");

                    b.Property<string>("SourceUri")
                        .HasColumnType("text")
                        .HasColumnName("source_uri");

                    b.Property<int>("StorageId")
                        .HasColumnType("integer")
                        .HasColumnName("storage_id");

                    b.Property<int>("Zoom")
                        .HasColumnType("integer")
                        .HasColumnName("zoom");

                    b.HasKey("ShotId");

                    b.HasIndex("AlbumId");

                    b.HasIndex("LocationId");

                    b.HasIndex("MD5")
                        .IsUnique();

                    b.HasIndex("StorageId");

                    b.ToTable("shots");
                });

            modelBuilder.Entity("Data.ShotComment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AuthorId")
                        .HasColumnType("integer")
                        .HasColumnName("author_id");

                    b.Property<string>("AuthorUsername")
                        .HasColumnType("text")
                        .HasColumnName("author_username");

                    b.Property<int>("ShotId")
                        .HasColumnType("integer")
                        .HasColumnName("shot_id");

                    b.Property<string>("Text")
                        .HasColumnType("text")
                        .HasColumnName("text");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("time");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.HasIndex("ShotId");

                    b.ToTable("shot_comments");
                });

            modelBuilder.Entity("Data.ShotStorage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AuthToken")
                        .HasColumnType("text")
                        .HasColumnName("auth_token");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("provider");

                    b.Property<string>("RefreshToken")
                        .HasColumnType("text")
                        .HasColumnName("refresh_token");

                    b.Property<string>("Root")
                        .HasColumnType("text")
                        .HasColumnName("root");

                    b.Property<int>("UserId")
                        .HasColumnType("integer")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("storages");
                });

            modelBuilder.Entity("Data.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("UserId"));

                    b.Property<string>("Email")
                        .HasColumnType("text")
                        .HasColumnName("email");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("text")
                        .HasColumnName("password_hash");

                    b.Property<string>("Username")
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("UserId");

                    b.ToTable("users");
                });

            modelBuilder.Entity("PersonShot", b =>
                {
                    b.Property<int>("PersonsPersonId")
                        .HasColumnType("integer");

                    b.Property<int>("ShotsShotId")
                        .HasColumnType("integer");

                    b.HasKey("PersonsPersonId", "ShotsShotId");

                    b.HasIndex("ShotsShotId");

                    b.ToTable("PersonShot");
                });

            modelBuilder.Entity("Data.Album", b =>
                {
                    b.HasOne("Data.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Data.AlbumComment", b =>
                {
                    b.HasOne("Data.Album", "Album")
                        .WithMany("AlbumComments")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Data.User", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Album");

                    b.Navigation("Author");
                });

            modelBuilder.Entity("Data.Shot", b =>
                {
                    b.HasOne("Data.Album", "Album")
                        .WithMany("Shots")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Data.Location", "Location")
                        .WithMany()
                        .HasForeignKey("LocationId");

                    b.HasOne("Data.ShotStorage", "Storage")
                        .WithMany("Shots")
                        .HasForeignKey("StorageId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Album");

                    b.Navigation("Location");

                    b.Navigation("Storage");
                });

            modelBuilder.Entity("Data.ShotComment", b =>
                {
                    b.HasOne("Data.User", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Data.Shot", "Shot")
                        .WithMany("ShotComments")
                        .HasForeignKey("ShotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Author");

                    b.Navigation("Shot");
                });

            modelBuilder.Entity("Data.ShotStorage", b =>
                {
                    b.HasOne("Data.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("PersonShot", b =>
                {
                    b.HasOne("Data.Person", null)
                        .WithMany()
                        .HasForeignKey("PersonsPersonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Data.Shot", null)
                        .WithMany()
                        .HasForeignKey("ShotsShotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Data.Album", b =>
                {
                    b.Navigation("AlbumComments");

                    b.Navigation("Shots");
                });

            modelBuilder.Entity("Data.Shot", b =>
                {
                    b.Navigation("ShotComments");
                });

            modelBuilder.Entity("Data.ShotStorage", b =>
                {
                    b.Navigation("Shots");
                });
#pragma warning restore 612, 618
        }
    }
}
