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

            modelBuilder.Entity("svema.Data.Comment", b =>
                {
                    b.Property<int>("CommentId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("CommentId"));

                    b.Property<int?>("AuthorUserId")
                        .HasColumnType("integer");

                    b.Property<int>("FilmId")
                        .HasColumnType("integer");

                    b.Property<int>("ShotId")
                        .HasColumnType("integer");

                    b.Property<string>("Text")
                        .HasColumnType("text");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("CommentId");

                    b.HasIndex("AuthorUserId");

                    b.ToTable("Comment");
                });

            modelBuilder.Entity("svema.Data.Film", b =>
                {
                    b.Property<int>("FilmId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("FilmId"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("DatePrecision")
                        .HasColumnType("text");

                    b.Property<int?>("LocationId")
                        .HasColumnType("integer");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("FilmId");

                    b.HasIndex("LocationId");

                    b.ToTable("Films");
                });

            modelBuilder.Entity("svema.Data.Location", b =>
                {
                    b.Property<int>("LocationId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("LocationId"));

                    b.Property<float>("Latitude")
                        .HasColumnType("real");

                    b.Property<int>("LocationPrecisionMeters")
                        .HasColumnType("integer");

                    b.Property<float>("Longitude")
                        .HasColumnType("real");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("LocationId");

                    b.ToTable("Location");
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

                    b.ToTable("Person");
                });

            modelBuilder.Entity("svema.Data.Shot", b =>
                {
                    b.Property<int>("ShotId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("ShotId"));

                    b.Property<DateTime>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("FilmId")
                        .HasColumnType("integer");

                    b.Property<int?>("LocationId")
                        .HasColumnType("integer");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<byte[]>("Preview")
                        .HasColumnType("bytea");

                    b.Property<string>("SourceUri")
                        .HasColumnType("text");

                    b.HasKey("ShotId");

                    b.HasIndex("FilmId");

                    b.HasIndex("LocationId");

                    b.ToTable("Shots");
                });

            modelBuilder.Entity("svema.Data.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("UserId"));

                    b.HasKey("UserId");

                    b.ToTable("User");
                });

            modelBuilder.Entity("svema.Data.Comment", b =>
                {
                    b.HasOne("svema.Data.User", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorUserId");

                    b.Navigation("Author");
                });

            modelBuilder.Entity("svema.Data.Film", b =>
                {
                    b.HasOne("svema.Data.Location", "Location")
                        .WithMany()
                        .HasForeignKey("LocationId");

                    b.Navigation("Location");
                });

            modelBuilder.Entity("svema.Data.Shot", b =>
                {
                    b.HasOne("svema.Data.Film", "Film")
                        .WithMany()
                        .HasForeignKey("FilmId");

                    b.HasOne("svema.Data.Location", "Location")
                        .WithMany()
                        .HasForeignKey("LocationId");

                    b.Navigation("Film");

                    b.Navigation("Location");
                });
#pragma warning restore 612, 618
        }
    }
}
