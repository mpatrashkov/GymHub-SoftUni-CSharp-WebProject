﻿using System;
using System.ComponentModel.DataAnnotations;

namespace GymHub.Data.Models
{
    public class Country
    {
        public Country()
        {
            this.Id = Guid.NewGuid().ToString();
        }
        [Key]
        public string Id { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }
    }
}
