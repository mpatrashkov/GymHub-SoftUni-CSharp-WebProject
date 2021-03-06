﻿using AutoMapper;
using GymHub.Data.Models;
using GymHub.Web.Models.InputModels;
using GymHub.Web.Models.ViewModels;

namespace GymHub.Automapper.AutomapperProfiles
{
    public class ProducProfile : Profile
    {
        public ProducProfile()
        {
            CreateMap<Product, ProductViewModel>();

            CreateMap<Product, ProductInfoViewModel>();

            CreateMap<AddProductInputModel, Product>()
                .ForMember(x => x.AdditionalImages, opt => opt.Ignore())
                .ForMember(x => x.Id, opt => opt.Ignore())
                .ReverseMap();
        }
    }
}
