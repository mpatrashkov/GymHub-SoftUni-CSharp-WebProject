﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GymHub.Web.Models.InputModels
{
    public class AddCategoryInputModel
    {
        [Required]
        public string Name { get; set; }
    }
}
