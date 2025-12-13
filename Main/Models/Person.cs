using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Svema.Models
{
    [Table("persons")]
    public partial class Person
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("first_name")]
        public string FirstName { get; set; }
        [Column("last_name")]
        public string LastName { get; set; }
        [Column("profile_photo_id")]
        public int? ProfilePhotoId { get; set; }

        [NotMapped]
        public string? Name => string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName)
            ? null
            : $"{FirstName} {LastName}".Trim();
    }
}
