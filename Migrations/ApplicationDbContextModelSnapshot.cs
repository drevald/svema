﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using svema.Data;

#nullable disable

namespace svema.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

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

            modelBuilder.Entity("svema.Data.Album", b =>
                {
                    b.Property<int>("AlbumId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("AlbumId"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DatePrecision")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("AlbumId");

                    b.ToTable("Albums");
                });

            modelBuilder.Entity("svema.Data.AlbumLocation", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AlbumId")
                        .HasColumnType("integer");

                    b.Property<int>("LocationId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("LocationId");

                    b.ToTable("AlbumLocations");
                });

            modelBuilder.Entity("svema.Data.Location", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<float>("Latitude")
                        .HasColumnType("real");

                    b.Property<int>("LocationPrecisionMeters")
                        .HasColumnType("integer");

                    b.Property<float>("Longitude")
                        .HasColumnType("real");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Locations");
                });

            modelBuilder.Entity("svema.Data.Person", b =>
                {
                    b.Property<int>("PersonId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("PersonId"));

                    b.Property<string>("FirstName")
                        .HasColumnType("text");

                    b.Property<string>("LastName")
                        .HasColumnType("text");

                    b.HasKey("PersonId");

                    b.ToTable("Persons");
                });

            modelBuilder.Entity("svema.Data.Shot", b =>
                {
                    b.Property<int>("ShotId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("ShotId"));

                    b.Property<int>("AlbumId")
                        .HasColumnType("integer");

                    b.Property<string>("ContentType")
                        .HasColumnType("text");

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("LocationId")
                        .HasColumnType("integer");

                    b.Property<string>("MD5")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<byte[]>("Preview")
                        .HasColumnType("bytea");

                    b.Property<string>("SourceUri")
                        .HasColumnType("text");

                    b.HasKey("ShotId");

                    b.HasIndex("AlbumId");

                    b.HasIndex("LocationId");

                    b.HasIndex("MD5")
                        .IsUnique();

                    b.ToTable("Shots");
                });

            modelBuilder.Entity("svema.Data.ShotComment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int?>("AlbumId")
                        .HasColumnType("integer");

                    b.Property<int?>("AuthorUserId")
                        .HasColumnType("integer");

                    b.Property<int>("ShotId")
                        .HasColumnType("integer");

                    b.Property<string>("Text")
                        .HasColumnType("text");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("AuthorUserId");

                    b.HasIndex("ShotId");

                    b.ToTable("ShotComment");
                });

            modelBuilder.Entity("svema.Data.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("UserId"));

                    b.HasKey("UserId");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("PersonShot", b =>
                {
                    b.HasOne("svema.Data.Person", null)
                        .WithMany()
                        .HasForeignKey("PersonsPersonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("svema.Data.Shot", null)
                        .WithMany()
                        .HasForeignKey("ShotsShotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("svema.Data.AlbumLocation", b =>
                {
                    b.HasOne("svema.Data.Album", "Album")
                        .WithMany("AlbumLocations")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("svema.Data.Location", "Location")
                        .WithMany("AlbumLocations")
                        .HasForeignKey("LocationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Album");

                    b.Navigation("Location");
                });

            modelBuilder.Entity("svema.Data.Shot", b =>
                {
                    b.HasOne("svema.Data.Album", "Album")
                        .WithMany("Shots")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("svema.Data.Location", "Location")
                        .WithMany()
                        .HasForeignKey("LocationId");

                    b.Navigation("Album");

                    b.Navigation("Location");
                });

            modelBuilder.Entity("svema.Data.ShotComment", b =>
                {
                    b.HasOne("svema.Data.Album", null)
                        .WithMany("Comments")
                        .HasForeignKey("AlbumId");

                    b.HasOne("svema.Data.User", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorUserId");

                    b.HasOne("svema.Data.Shot", "Shot")
                        .WithMany("Comments")
                        .HasForeignKey("ShotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Author");

                    b.Navigation("Shot");
                });

            modelBuilder.Entity("svema.Data.Album", b =>
                {
                    b.Navigation("AlbumLocations");

                    b.Navigation("Comments");

                    b.Navigation("Shots");
                });

            modelBuilder.Entity("svema.Data.Location", b =>
                {
                    b.Navigation("AlbumLocations");
                });

            modelBuilder.Entity("svema.Data.Shot", b =>
                {
                    b.Navigation("Comments");
                });
#pragma warning restore 612, 618
        }
    }
}
