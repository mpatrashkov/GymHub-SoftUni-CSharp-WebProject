﻿using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GymHub.Services.Common
{
    public static class IQueryableExtension
    {
        public static IQueryable<EntityType> IgnoreAllQueryFilters<EntityType>(this DbSet<EntityType> source, bool ignore = false) where EntityType : class
        {
            if (ignore == true) return source.IgnoreQueryFilters();
            return source.AsQueryable();
        } 
    }
}
